using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;


namespace ObjectPrinting
{
    public class PrintingConfig<TOwner>
    {
        public readonly HashSet<Type> ExcludedTypes = [];
        public readonly HashSet<MemberInfo> ExcludedMember = [];
        public readonly Dictionary<MemberInfo, int> TrimmedMembers = new();
        public readonly Dictionary<Type, CultureInfo> CulturesForTypes = new();
        internal readonly Dictionary<Type, Delegate> CustomTypeSerializers = new();
        internal readonly Dictionary<MemberInfo, Delegate> CustomMemberSerializers = new();
        internal int? TrimStringLength;
        public int MaxNestingLevel { get; set; } = 5;

        public PrintingConfig<TOwner> Excluding<TMember>()
        {
            ExcludedTypes.Add(typeof(TMember));
            return this;
        }

        public PrintingConfig<TOwner> Excluding<TMember>(Expression<Func<TOwner, TMember>> memberSelector)
        {
            ExcludedMember.Add(GetMember(memberSelector));
            return this;
        }

        public MemberPrintingConfig<TOwner, TMember> Printing<TMember>()
            => new(this);

        public StintingPrintConfig<TOwner> Printing(Expression<Func<TOwner, string>> stringMemberSelector)
            => new(this, GetMember(stringMemberSelector));


        public string PrintToString(TOwner? obj)
            => new Serializer<TOwner>(this).SerializeObject(obj);
        

        private static MemberInfo GetMember<TMember>(Expression<Func<TOwner, TMember>> selector)
        {
            if (selector.Body is MemberExpression m) return m.Member;
            throw new ArgumentException("Member selector must be a simple member access.", nameof(selector));
        }
    }
}