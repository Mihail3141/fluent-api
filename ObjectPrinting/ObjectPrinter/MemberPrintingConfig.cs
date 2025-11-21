using System;
using System.Globalization;
using System.Reflection;

namespace ObjectPrinting;

public class MemberPrintingConfig<TOwner, TMemberType>(
    PrintingConfig<TOwner> printingConfig, MemberInfo? memberInfo = null)
{
    readonly PrintingConfig<TOwner> PrintingConfig = printingConfig;
    readonly MemberInfo? MemberInfo = memberInfo;

    public PrintingConfig<TOwner> Using(Func<TMemberType, string> printingMethod)
    {
        if (MemberInfo is null)
            PrintingConfig.CustomTypeSerializers[typeof(TMemberType)] = printingMethod;
        else
            PrintingConfig.CustomMemberSerializers[MemberInfo] = printingMethod;
        return PrintingConfig;
    }
    
    public PrintingConfig<TOwner> Using(CultureInfo culture)
    {
        // Ограничим область применения: только типовой канал (без конкретного члена)
        if (MemberInfo is not null)
            throw new InvalidOperationException(
                "Using(CultureInfo) доступен только для SetPrintingFor<T>(), а не для конкретного члена.");

        // Применяем культуру ко всему типу TMemberType
        PrintingConfig.CulturesForTypes[typeof(TMemberType)] = culture;
        return PrintingConfig;
    }
}