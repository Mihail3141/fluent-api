using System.Reflection;

namespace ObjectPrinting;

public class StintingPrintConfig<TOwner>(PrintingConfig<TOwner> printingConfig, MemberInfo memberInfo)
    : MemberPrintingConfig<TOwner, string>(printingConfig, memberInfo)
{
    public PrintingConfig<TOwner> TrimmedToLength(int length)
    {
        printingConfig.TrimmedMembers[memberInfo] = length;
        return PrintingConfig;
    }
}