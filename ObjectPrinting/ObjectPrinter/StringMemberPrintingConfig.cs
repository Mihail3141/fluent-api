using System;
using System.Reflection;

namespace ObjectPrinting.Solved;

public class StringMemberPrintingConfig<TOwner, TMember>
{
    private readonly PrintingConfig<TOwner> _parent;
    private readonly MemberInfo _member;

    internal StringMemberPrintingConfig(PrintingConfig<TOwner> parent, MemberInfo member)
    {
        _parent = parent;
        _member = member;
    }

    // Специфично для string
    public PrintingConfig<TOwner> TrimmedToLength(int maxLength)
    {
        if (typeof(TMember) != typeof(string))
            throw new InvalidOperationException("TrimmedToLength доступен только для строковых свойств.");

        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        _parent.TrimmedMembers[_member] = maxLength;
        return _parent;
    }
}