using System.Reflection;
using System.Reflection.Emit;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Random;

namespace BalatroEffects;

public static class EasePatches
{
    public static void AnimateEase(Control instance, Vector2 targetScale)
    {
        const double duration = 0.12f;

        var trv = Traverse.Create(instance);
        var hoverTweenField = trv.Field<Tween>("_hoverTween");

        hoverTweenField.Value?.Kill();

        instance.Scale *= 0.88f;
        instance.Rotation = 0.35f * (Rng.Chaotic.NextBool() ? 1f : -1f);

        var rotTween = instance.CreateTween();
        rotTween
            .TweenProperty(instance, "rotation", 0f, duration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);

        var tween = instance.CreateTween();
        tween
            .TweenProperty(instance, "scale", targetScale, duration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);

        hoverTweenField.Value = tween;
    }

    static readonly MethodInfo getHoverScale = AccessTools.PropertyGetter(
        typeof(NCardHolder),
        "HoverScale"
    );
    static readonly MethodInfo createHoverTipsMethod = AccessTools.Method(
        typeof(NCardHolder),
        "CreateHoverTips"
    );
    static readonly MethodInfo animateEaseMethod = AccessTools.Method(
        typeof(EasePatches),
        nameof(AnimateEase)
    );

    [HarmonyPatch(typeof(NCardHolder), "DoCardHoverEffects")]
    static class NCardHolderEase
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new CodeMatcher(instructions);

            // insert after `CreateHoverTips()`
            codes.Start().MatchStartForward(new CodeMatch(i => i.Calls(createHoverTipsMethod)));
            if (codes.IsValid)
            {
                codes.InsertAfter(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Callvirt, getHoverScale),
                    new CodeInstruction(OpCodes.Call, animateEaseMethod)
                );
            }
            else
            {
                MainFile.Logger.Error("Failed to patch DoCardHoverEffects in NCardHolder");
            }

            return codes.InstructionEnumeration();
        }
    }

    [HarmonyPatch(typeof(NHandCardHolder), "DoCardHoverEffects")]
    static class NHandCardHolderEase
    {
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new CodeMatcher(instructions);

            // insert after `CreateHoverTips()`
            codes.Start().MatchStartForward(new CodeMatch(i => i.Calls(createHoverTipsMethod)));
            if (codes.IsValid)
            {
                codes.InsertAfter(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Callvirt, getHoverScale),
                    new CodeInstruction(OpCodes.Call, animateEaseMethod)
                );
            }
            else
            {
                MainFile.Logger.Error("Failed to patch DoCardHoverEffects in NHandCardHolder");
            }

            return codes.InstructionEnumeration();
        }
    }
}
