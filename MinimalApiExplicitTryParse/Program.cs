using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// This will not work as there is an explicit implementation whose method name is
// System.IParsable<TTypedGuid>.TryParse but not System.IParsable<MyTypedId>.TryParse
app.MapGet("/{myId}", ([FromRoute] MyTypedId myId) => $"Called with {myId.Guid}");

// This will not work as there is an explicit implementation whose method name is
// System.IParsable<IdSegmentExplicit<TId>>.TryParse but not System.IParsable<IdSegmentImplicit<MyTypedId>>.TryParse
// app.MapGet("/{myId}", ([FromRoute] IdSegmentExplicit<MyTypedId> myId) => $"Called with {myId.Id}");

// This will work as there is a public static method TryParse with the correct arguments where there it does not matter
// whether IParsable is implemented or not.
// app.MapGet("/{myId}", ([FromRoute] IdSegmentImplicit<MyTypedId> myId) => $"Called with {myId.Id}");

app.Run();

// ---

public interface ITypedGuid<TTypedGuid> : IParsable<TTypedGuid>
    where TTypedGuid : struct, ITypedGuid<TTypedGuid>
{
    Guid Guid { get; }

    static abstract implicit operator TTypedGuid(Guid guid);

    /// <inheritdoc />
    static TTypedGuid IParsable<TTypedGuid>.Parse(string s, IFormatProvider? provider) => Guid.Parse(s, provider);

    /// <inheritdoc />
    static bool IParsable<TTypedGuid>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out TTypedGuid result)
    {
        if (Guid.TryParse(s, provider, out var guid))
        {
            result = guid;
            return true;
        }

        result = default;
        return false;
    }
}

public readonly struct MyTypedId : ITypedGuid<MyTypedId>
{
    private MyTypedId(Guid guid) => Guid = guid;

    /// <inheritdoc />
    public Guid Guid { get; }

    /// <inheritdoc />
    public static implicit operator MyTypedId(Guid guid) => new(guid);

    /// <inheritdoc />
    public override string ToString() => Guid.ToString();
}

public sealed class IdSegmentImplicit<TId> : IParsable<IdSegmentImplicit<TId>>
    where TId : struct, IParsable<TId>
{
    public readonly TId Id;
    private IdSegmentImplicit(TId id) => Id = id;

    /// <inheritdoc />
    public static IdSegmentImplicit<TId> Parse(string s, IFormatProvider? provider)
    {
        return new IdSegmentImplicit<TId>(TId.Parse(s, provider));
    }

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out IdSegmentImplicit<TId> result)
    {
        if (TId.TryParse(s, provider, out var id))
        {
            result = new IdSegmentImplicit<TId>(id);
            return true;
        }

        result = default;
        return false;
    }
}

public sealed class IdSegmentExplicit<TId> : IParsable<IdSegmentExplicit<TId>>
    where TId : struct, IParsable<TId>
{
    public readonly TId Id;
    private IdSegmentExplicit(TId id) => Id = id;

    /// <inheritdoc />
    static IdSegmentExplicit<TId> IParsable<IdSegmentExplicit<TId>>.Parse(string s, IFormatProvider? provider)
    {
        return new IdSegmentExplicit<TId>(TId.Parse(s, provider));
    }

    /// <inheritdoc />
    static bool IParsable<IdSegmentExplicit<TId>>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out IdSegmentExplicit<TId> result)
    {
        if (TId.TryParse(s, provider, out var id))
        {
            result = new IdSegmentExplicit<TId>(id);
            return true;
        }

        result = default;
        return false;
    }
}

// Possible solution for ParameterBindingMethodCache

internal sealed class ParameterBindingMethodCache
{
    // existing code ...

    public Func<ParameterExpression, Expression, Expression>? FindTryParseMethod(Type type)
    {
        Func<ParameterExpression, Expression, Expression>? Finder(Type type)
        {
            MethodInfo? methodInfo;

            if (TryGetImplementedIParsableTryParseMethod(type, out var iParsableTryParseMethod))
            {
                // existing code in if (TryGetExplicitIParsableTryParseMethod(type, out var explicitIParsableTryParseMethod)) { } ...
            }

            // further existing code...

            return null; // just to compile
        }

        // return _stringMethodCallCache.GetOrAdd(type, Finder);
        return null; // just to compile
    }

    private static bool TryGetImplementedIParsableTryParseMethod(Type type, [MaybeNullWhen(false)] out MethodInfo methodInfo)
    {
        var desiredIParsableType = typeof(IParsable<>).MakeGenericType(type);

        if (type.IsAssignableTo(desiredIParsableType))
        {
            var interfaceMapping = type.GetInterfaceMap(desiredIParsableType);
            var index = Array.FindLastIndex(interfaceMapping.InterfaceMethods, m => m.Name == "TryParse");
            if (index != -1)
            {
                methodInfo = interfaceMapping.TargetMethods[index];
                return true;
            }
        }

        methodInfo = null;
        return false;
    }
}
