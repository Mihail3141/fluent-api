using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ObjectPrinting;

internal sealed class Serializer<TOwner>
{
    private readonly PrintingConfig<TOwner> config;
    private readonly HashSet<object> visited = new(ReferenceEqualityComparer.Instance);

    private static readonly Type[] FinalTypes =
    {
        typeof(string), typeof(DateTime), typeof(TimeSpan),
        typeof(Guid), typeof(decimal),
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(float), typeof(double), typeof(bool), typeof(char)
    };

    internal Serializer(PrintingConfig<TOwner> config) => this.config = config;

    internal string SerializeObject(object? obj)
    {
        var sb = new StringBuilder();
        Serialize(obj, sb, 0, parentMember: null);
        return sb.ToString();
    }

    private void Serialize(object? obj, StringBuilder sb, int level, MemberInfo? parentMember)
    {
        if (obj is null)
        {
            sb.AppendLine("null");
            return;
        }

        var type = obj.GetType();

        // Исключённые типы
        if (config.ExcludedTypes.Contains(type))
        {
            sb.AppendLine("[Excluded Type]");
            return;
        }

        // Ограничение глубины
        if (level >= config.MaxNestingLevel)
        {
            sb.AppendLine($"[{type.Name} ...]");
            return;
        }

        // Циклические ссылки
        if (!type.IsValueType)
        {
            if (visited.Contains(obj))
            {
                sb.AppendLine($"[CyclicRef {type.Name}]");
                return;
            }

            visited.Add(obj);
        }

        // 1) Приоритет: кастомный сериализатор ТИПА (включая массивы и любые коллекции)
        if (config.CustomTypeSerializers.TryGetValue(type, out var typeSer))
        {
            var text = InvokeSerializer(typeSer, obj);
            sb.Append(ApplyTrimming(text, parentMember));
            return;
        }

        // 2) Финальные типы и форматирование с культурой
        if (IsFinal(type))
        {
            sb.Append(FormatScalar(obj, type, parentMember));
            return;
        }

        // 3) Коллекции: словари
        if (obj is IDictionary dict)
        {
            sb.AppendLine(type.Name);
            foreach (DictionaryEntry entry in dict)
            {
                Indent(sb, level + 1);
                sb.Append("Key = ");
                Serialize(entry.Key, sb, level + 1, null);

                Indent(sb, level + 1);
                sb.Append("Value = ");
                Serialize(entry.Value, sb, level + 1, null);
            }

            return;
        }

        // 4) Коллекции: перечислимые (массивы, списки, и пр.) — если нет кастомного типа-сериализатора
        if (obj is IEnumerable enumerable && type != typeof(string))
        {
            sb.AppendLine(type.Name);
            int i = 0;
            foreach (var item in enumerable)
            {
                Indent(sb, level + 1);
                sb.Append($"[{i}] = ");
                Serialize(item, sb, level + 1, null);
                i++;
            }

            return;
        }

        // 5) Сложные объекты: публичные свойства и поля
        sb.AppendLine(type.Name);

        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!prop.CanRead) continue;
            if (config.ExcludedMembers.Contains(prop)) continue;
            if (config.ExcludedTypes.Contains(prop.PropertyType)) continue;

            Indent(sb, level + 1);
            sb.Append(prop.Name);
            sb.Append(" = ");

            if (config.CustomMemberSerializers.TryGetValue(prop, out var memberSer))
            {
                var value = SafeGet(() => prop.GetValue(obj));
                var text = value is null ? "null" : InvokeSerializer(memberSer, value!);
                sb.AppendLine(ApplyTrimming(text, parentMember: prop).TrimEnd('\r', '\n'));
                continue;
            }

            var propValue = SafeGet(() => prop.GetValue(obj));
            Serialize(propValue, sb, level + 1, prop);
        }

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (config.ExcludedMembers.Contains(field)) continue;
            if (config.ExcludedTypes.Contains(field.FieldType)) continue;

            Indent(sb, level + 1);
            sb.Append(field.Name);
            sb.Append(" = ");

            if (config.CustomMemberSerializers.TryGetValue(field, out var memberSer))
            {
                var value = SafeGet(() => field.GetValue(obj));
                var text = value is null ? "null" : InvokeSerializer(memberSer, value!);
                sb.AppendLine(ApplyTrimming(text, parentMember: field).TrimEnd('\r', '\n'));
                continue;
            }

            var fieldValue = SafeGet(() => field.GetValue(obj));
            Serialize(fieldValue, sb, level + 1, field);
        }
    }


    private static void Indent(StringBuilder sb, int level)
        => sb.Append(new string('\t', level));

    private static bool IsFinal(Type t) => FinalTypes.Contains(t) || t.IsEnum;

    private string FormatScalar(object value, Type type, MemberInfo? parentMember)
    {
        // Member-level serializer has higher priority already handled before call.
        if (config.CustomTypeSerializers.TryGetValue(type, out var del))
        {
            var s = InvokeSerializer(del, value);
            return ApplyTrimming(s, parentMember);
        }

        if (type == typeof(string))
        {
            var s = (string)value;
            return ApplyTrimming(s, parentMember);
        }

        // Culture for numeric types and DateTime/decimal/float/double/etc.
        if (config.CulturesForTypes.TryGetValue(type, out var culture))
        {
            if (value is IFormattable fmt)
                return fmt.ToString(null, culture) + Environment.NewLine;
        }

        return value + Environment.NewLine;
    }

    private string ApplyTrimming(string s, MemberInfo? parentMember)
    {
        int? length = null;
        if (parentMember != null && config.TrimmedMembers.TryGetValue(parentMember, out var memberLen))
            length = memberLen;
        else if (config.TrimStringLength.HasValue)
            length = config.TrimStringLength;

        if (length.HasValue && s != null && s.Length > length.Value)
            s = s.Substring(0, length.Value);

        return s.EndsWith(Environment.NewLine) ? s : s + Environment.NewLine;
    }

    private static string InvokeSerializer(Delegate del, object value)
        => (string)del.DynamicInvoke(value)!;

    private static object? SafeGet(Func<object?> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    // Reference equality comparer for visited set
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}