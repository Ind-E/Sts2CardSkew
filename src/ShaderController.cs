using System.Reflection.Emit;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using static Godot.CanvasItem;

namespace BalatroEffects;

public partial class ShaderController
{
    private static readonly StringName _xRotKey = "x_rot";
    private static readonly StringName _yRotKey = "y_rot";
    private static readonly StringName _effectModeKey = "effect_mode";
    private static readonly StringName _intensityKey = "intensity";
    private static readonly StringName _seedKey = "seed";

    private static readonly Shader EffectsShader = GD.Load<Shader>(
        "res://BalatroEffects/shaders/balatro_effects.gdshader"
    );

    public static void ApplyShader(NCard cardRoot)
    {
        if (
            cardRoot.HasNode("BalatroShaderViewportContainer")
            || cardRoot.GetNodeOrNull<Control>("CardContainer") is not Control cardContainer
            || cardRoot?.Model?.Id?.ToString() is not string cardId
        )
        {
            return;
        }

        var size = new Vector2I(370, 480);

        var mat = new ShaderMaterial { Shader = EffectsShader };
        float seed = cardRoot.GetHashCode() % 10000 / 10.0f;
        mat.SetShaderParameter(_seedKey, seed);

        var viewportContainer = new ShaderContainer
        {
            Material = mat,
            Name = "BalatroShaderViewportContainer",
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Size = size,
            Stretch = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = -size / 2,
            PivotOffset = size / 2,
            CardId = cardId,
        };

        var viewport = new SubViewport { TransparentBg = true, Size = size };

        cardContainer.Position = size / 2;

        cardRoot.RemoveChild(cardContainer);
        cardRoot.AddChild(viewportContainer);
        viewportContainer.AddChild(viewport);
        viewport.AddChild(cardContainer);

        int savedEffect = Config.GetEffect(cardId);
        mat.SetShaderParameter(_effectModeKey, savedEffect);

        double savedIntensity = Config.GetIntensity(savedEffect);
        mat.SetShaderParameter(_intensityKey, savedIntensity);
    }

    private partial class ShaderContainer : SubViewportContainer
    {
        private const float MaxTilt = 16.0f;
        private const float LerpSpeed = 0.2f;

        private Control? cardRoot;
        private NCardHolder? cardHolder;
        private int lastAppliedEffect = -1;
        private double lastAppliedIntensity = -1;

        public string? CardId;
        public ShaderMaterial? mat;

        public override void _Ready()
        {
            cardRoot = GetParent<Control>();
            mat = Material as ShaderMaterial;

            if (string.IsNullOrEmpty(CardId) && cardRoot is NCard card)
            {
                CardId = card.Model?.Id?.ToString();
            }
        }

        private void UpdateHolderReference()
        {
            NCardHolder? foundHolder = null;
            for (Node? curr = GetParent(); curr is not null; curr = curr.GetParent())
            {
                if (curr is NCardHolder h)
                {
                    foundHolder = h;
                    break;
                }
            }

            if (cardHolder == foundHolder || mat is null)
                return;

            cardHolder = foundHolder;

            mat.SetShaderParameter(_xRotKey, 0f);
            mat.SetShaderParameter(_yRotKey, 0f);
        }

        private void CheckForIdUpdate()
        {
            if (cardRoot is NCard nCard)
            {
                string? currentModelId = nCard.Model?.Id?.ToString();
                if (currentModelId != CardId)
                {
                    CardId = currentModelId;
                }
            }
        }

        static readonly AccessTools.FieldRef<NClickableControl, bool> IsHovered =
            AccessTools.FieldRefAccess<NClickableControl, bool>("_isHovered");

        public override void _Process(double delta)
        {
            if (mat is null || cardRoot is null)
                return;

            CheckForIdUpdate();

            if (!string.IsNullOrEmpty(CardId))
            {
                int savedEffect = Config.GetEffect(CardId);
                if (savedEffect != lastAppliedEffect)
                {
                    mat.SetShaderParameter(_effectModeKey, savedEffect);
                    lastAppliedEffect = savedEffect;
                }

                double savedIntensity = Config.GetIntensity(savedEffect);
                if (savedIntensity != lastAppliedIntensity)
                {
                    mat.SetShaderParameter(_intensityKey, savedIntensity);
                    lastAppliedIntensity = savedIntensity;
                }
            }

            UpdateHolderReference();

            if (!IsInstanceValid(cardHolder))
                return;

            float targetX = 0;
            float targetY = 0;

            bool hovered =
                cardHolder is NHandCardHolder { ZIndex: > 0 }
                || (cardHolder.Hitbox is { IsEnabled: true } && IsHovered(cardHolder.Hitbox));

            if (hovered)
            {
                Vector2 offset = cardRoot.GetGlobalMousePosition() - cardRoot.GlobalPosition;
                Vector2 scale = cardRoot.GetGlobalTransform().Scale.Max(0.01f) * 256f;

                targetX = offset.Y / scale.X * -MaxTilt;
                targetY = offset.X / scale.Y * MaxTilt;
            }

            targetX = Mathf.Clamp(targetX, -MaxTilt, MaxTilt);
            targetY = Mathf.Clamp(targetY, -MaxTilt, MaxTilt);

            float curX = (float)mat.GetShaderParameter("x_rot");
            float curY = (float)mat.GetShaderParameter("y_rot");

            mat.SetShaderParameter("x_rot", Mathf.Lerp(curX, targetX, LerpSpeed));
            mat.SetShaderParameter("y_rot", Mathf.Lerp(curY, targetY, LerpSpeed));
        }
    }

    [HarmonyPatch(typeof(NCard), nameof(NCard.ActivateRewardScreenGlow))]
    public static class CardGlowBelowViewportPatch
    {
        private static readonly AccessTools.FieldRef<NCard, Node> RareGlowRef =
            AccessTools.FieldRefAccess<NCard, Node>("_rareGlow");

        private static readonly AccessTools.FieldRef<NCard, Node> UncommonGlowRef =
            AccessTools.FieldRefAccess<NCard, Node>("_uncommonGlow");

        [HarmonyPostfix]
        public static void Postfix(NCard __instance)
        {
            Control? body = __instance.Body;
            if (body == null)
                return;

            Node? cardRoot = body.GetParent()?.GetParent()?.GetParent();
            if (cardRoot == null)
                return;

            GpuParticles2D glow = (GpuParticles2D)(
                RareGlowRef(__instance) ?? UncommonGlowRef(__instance)
            );

            if (glow != null && GodotObject.IsInstanceValid(glow) && glow.GetParent() == body)
            {
                body.RemoveChild(glow);

                cardRoot.AddChildSafely(glow);
                cardRoot.MoveChild(glow, 0);
            }
        }
    }
}
