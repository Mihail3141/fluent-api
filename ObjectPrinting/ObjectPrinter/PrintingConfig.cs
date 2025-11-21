using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using ObjectPrinting.Solved;

namespace ObjectPrinting
{
    public class PrintingConfig<TOwner>
    {
        public HashSet<Type> ExcludedTypes = [];
        public HashSet<MemberInfo> ExcludedMembers = [];
        public readonly Dictionary<MemberInfo, int> TrimmedMembers = new();
        public readonly Dictionary<Type, CultureInfo> CulturesForTypes = new();
        internal readonly Dictionary<Type, Delegate> CustomTypeSerializers = new();
        internal readonly Dictionary<MemberInfo, Delegate> CustomMemberSerializers = new();
        internal int? TrimStringLength;

        private int maxNestingLevel = 10;
        // public Dictionary<Type, Delegate> typeSerializer = new ();
        // public Dictionary<Type, CultureInfo> cultureSerializer = new ();
        // public List<MemberInfo> ExcludedMember = new();

        public int MaxNestingLevel
        {
            get => maxNestingLevel;
            private set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
                maxNestingLevel = value;
            }
        }

        public PrintingConfig<TOwner> Exclude<TMemberType>()
        {
            ExcludedTypes.Add(typeof(TMemberType));
            return this;
        }
        
        public PrintingConfig<TOwner> Exclude<TMemberType>(Expression<Func<TOwner, TMemberType>> memberSelector)
        {
            var member = GetMember(memberSelector);
            ExcludedMembers.Add(member);
            return this;
        }
        
        public MemberPrintingConfig<TOwner, TMemberType> SetPrintingFor<TMemberType>()
            => new(this);

        public MemberPrintingConfig<TOwner, TMemberType> SetPrintingFor<TMemberType>(
            Expression<Func<TOwner, TMemberType>> memberSelector)
            => new(this, GetMember(memberSelector));
        
        public PrintingConfig<TOwner> SetCulture<TNumeric>(CultureInfo culture)
        {
            CulturesForTypes[typeof(TNumeric)] = culture;
            return this;
        }
        
        public PrintingConfig<TOwner> TrimStringsTo(int maxLength)
        {
            if (maxLength < 0) throw new ArgumentOutOfRangeException(nameof(maxLength));
            TrimStringLength = maxLength;
            return this;
        }
        
        public PrintingConfig<TOwner> Trim<TMember>(Expression<Func<TOwner, TMember>> memberSelector, int maxLength)
        {
            if (maxLength < 0) throw new ArgumentOutOfRangeException(nameof(maxLength));
            var member = GetMember(memberSelector);
            TrimmedMembers[member] = maxLength;
            return this;
        }
        
        public PrintingConfig<TOwner> SetSerializationDepth(int depth)
        {
            MaxNestingLevel = depth;
            return this;
        }
        
        public string PrintToString(TOwner? obj)
            => new Serializer<TOwner>(this).SerializeObject(obj);
        
        private static MemberInfo GetMember<TMember>(Expression<Func<TOwner, TMember>> selector)
        {
            if (selector.Body is MemberExpression m) return m.Member;
            throw new ArgumentException("Member selector must be a simple member access.", nameof(selector));
        }
        
        

        private string PrintToString(object obj, int nestingLevel)
        {
            //TODO apply configurations
            if (obj == null)
                return "null" + Environment.NewLine;

            var finalTypes = new[]
            {
                typeof(int), typeof(double), typeof(float), typeof(string),
                typeof(DateTime), typeof(TimeSpan)
            };
            if (finalTypes.Contains(obj.GetType()))
                return obj + Environment.NewLine;

            var identation = new string('\t', nestingLevel + 1);
            var sb = new StringBuilder();
            var type = obj.GetType();
            sb.AppendLine(type.Name);
            foreach (var propertyInfo in type.GetProperties())
            {
                sb.Append(identation + propertyInfo.Name + " = " +
                          PrintToString(propertyInfo.GetValue(obj),
                              nestingLevel + 1));
            }

            return sb.ToString();
        }

        // public PrintingConfig<TOwner> ExcludeType<T>()
        // {
        //     excludedTypes.Add(typeof(T));
        //     return this;
        // }
        //
        // public PrintingConfig<TOwner> ExcludeProp<TProp>(Expression<Func<TOwner, TProp>> memberSelector)
        // {
        //     var expression = (MemberExpression)memberSelector.Body;
        //     ExcludedMember.Add(expression.Member);
        //     return this;
        // }
        //
        // public PropertyPrintingConfig<TOwner, TPropType> Printing<TPropType>()
        // {
        //     return new PropertyPrintingConfig<TOwner, TPropType>(this);
        // }
        //
        // public PropertyPrintingConfig<TOwner, TProp> Printing<TProp>(Expression<Func<TOwner, TProp>> memberSelector)
        // {
        //     return new PropertyPrintingConfig<TOwner, TProp>(this, memberSelector.Name);
        // }
    }
}