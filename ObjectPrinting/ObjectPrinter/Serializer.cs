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

    private static readonly Type[] TerminalTypes =
    [
        typeof(string), typeof(DateTime), typeof(TimeSpan),
        typeof(Guid), typeof(decimal),
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(float), typeof(double), typeof(bool), typeof(char)
    ];

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
        
        if (config.ExcludedTypes.Contains(type))
        {
            sb.AppendLine("[Excluded Type]");
            return;
        }
        
        if (level >= config.MaxNestingLevel)
        {
            sb.AppendLine($"[{FormatTypeName(type)} превышен уровень вложенности]");
            return;
        }
        
        if (!type.IsValueType)
        {
            if (visited.Contains(obj))
            {
                sb.AppendLine($"[CyclicRef {FormatTypeName(type)}]");
                return;
            }

            visited.Add(obj);
        }


        if (config.CustomTypeSerializers.TryGetValue(type, out var typeSer))
        {
            var text = InvokeSerializer(typeSer, obj);
            sb.Append(ApplyTrimming(text, parentMember));
            return;
        }


        if (TerminalTypes.Contains(type) || type.IsEnum)
        {
            sb.Append(FormatScalar(obj, type, parentMember));
            return;
        }
        
        if (obj is IDictionary dict)
        {
            sb.AppendLine(FormatTypeName(type));
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
        
        if (obj is IEnumerable enumerable && type != typeof(string))
        {
            sb.AppendLine(FormatTypeName(type));
            var i = 0;
            foreach (var item in enumerable)
            {
                Indent(sb, level + 1);
                sb.Append($"[{i}] = ");
                Serialize(item, sb, level + 1, null);
                i++;
            }

            return;
        }
        
        sb.AppendLine(FormatTypeName(type));

        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!prop.CanRead) continue;
            if (config.ExcludedMember.Contains(prop)) continue;
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
            if (config.ExcludedMember.Contains(field)) continue;
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
    

    private string FormatScalar(object value, Type type, MemberInfo? parentMember)
    {
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
        
        if (!config.CulturesForTypes.TryGetValue(type, out var culture))
            return value + Environment.NewLine;
        if (value is IFormattable fmt)
            return fmt.ToString(null, culture) + Environment.NewLine;

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
    
    private static string FormatTypeName(Type t)
    {
        if (!t.IsGenericType) return t.Name;

        var defName = t.Name;
        var backtick = defName.IndexOf('`');
        if (backtick > 0) defName = defName.Substring(0, backtick);

        var args = t.GetGenericArguments();
        var argNames = string.Join(", ", args.Select(FormatTypeName));
        return $"{defName}<{argNames}>";
    }
    
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}