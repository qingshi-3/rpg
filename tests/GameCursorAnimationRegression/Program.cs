using System.Reflection;

Type timelineType = ResolveTimelineType();
object timeline = Activator.CreateInstance(timelineType, 2, 12.0)
    ?? throw new InvalidOperationException("cursor animation timeline could not be constructed.");

MethodInfo start = RequiredMethod(timelineType, "Start", typeof(double));
MethodInfo resolveFrameIndex = RequiredMethod(timelineType, "ResolveFrameIndex", typeof(double));
MethodInfo isActive = RequiredMethod(timelineType, "IsActive", typeof(double));

start.Invoke(timeline, new object[] { 10.0 });

AssertEqual(0, InvokeInt(resolveFrameIndex, timeline, 10.0), "click animation should start at the first frame");
AssertTrue(InvokeBool(isActive, timeline, 10.0), "click animation should be active at the click timestamp");
AssertEqual(1, InvokeInt(resolveFrameIndex, timeline, 10.09), "click animation should advance to the second 12 fps frame");
AssertTrue(InvokeBool(isActive, timeline, 10.09), "click animation should remain active during its second frame");
AssertEqual(-1, InvokeInt(resolveFrameIndex, timeline, 10.18), "click animation should restore the static cursor after the final frame");
AssertTrue(!InvokeBool(isActive, timeline, 10.18), "click animation should be inactive after its frame budget");

start.Invoke(timeline, new object[] { 20.0 });
AssertEqual(0, InvokeInt(resolveFrameIndex, timeline, 20.0), "a later click should restart from the first frame");

Console.WriteLine("PASS game cursor click animation timeline");

static Type ResolveTimelineType()
{
    Type? type = Type.GetType("Rpg.Presentation.Common.GameCursorAnimationTimeline, rpg");
    if (type == null)
    {
        throw new InvalidOperationException("missing Rpg.Presentation.Common.GameCursorAnimationTimeline");
    }

    return type;
}

static MethodInfo RequiredMethod(Type type, string name, params Type[] parameterTypes)
{
    MethodInfo? method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, parameterTypes);
    if (method == null)
    {
        throw new InvalidOperationException($"missing method {type.FullName}.{name}");
    }

    return method;
}

static int InvokeInt(MethodInfo method, object target, double nowSeconds)
{
    object? value = method.Invoke(target, new object[] { nowSeconds });
    return value is int result
        ? result
        : throw new InvalidOperationException($"method {method.Name} should return int");
}

static bool InvokeBool(MethodInfo method, object target, double nowSeconds)
{
    object? value = method.Invoke(target, new object[] { nowSeconds });
    return value is bool result
        ? result
        : throw new InvalidOperationException($"method {method.Name} should return bool");
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected={expected} actual={actual}");
    }
}
