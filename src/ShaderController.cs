using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using static Godot.CanvasItem;

namespace BalatroEffects;

public partial class ShaderController
{
    private static readonly StringName XRotKey = "x_rot";
    private static readonly StringName YRotKey = "y_rot";
    private static readonly StringName EffectModeKey = "effect_mode";
    private static readonly StringName IntensityKey = "intensity";
    private static readonly StringName SeedKey = "seed";

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

        var size = new Vector2I(480, 480);

        var mat = new ShaderMaterial { Shader = EffectsShader };
        float seed = cardRoot.GetHashCode() % 10000 / 10.0f;
        mat.SetShaderParameter(SeedKey, seed);

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
        mat.SetShaderParameter(EffectModeKey, savedEffect);

        double savedIntensity = Config.GetIntensity(savedEffect);
        mat.SetShaderParameter(IntensityKey, savedIntensity);
    }

    private partial class ShaderContainer : SubViewportContainer
    {
        private const float MaxTilt = 16.0f;
        private const float LerpSpeed = 0.2f;

        private Control? _cardRoot;
        private NCardHolder? _cardHolder;
        private int _lastAppliedEffect = -1;
        private double _lastAppliedIntensity = -1;

        public string? CardId;
        public ShaderMaterial? mat;

        public override void _Ready()
        {
            _cardRoot = GetParent<Control>();
            mat = Material as ShaderMaterial;

            if (string.IsNullOrEmpty(CardId) && _cardRoot is NCard card)
            {
                CardId = card.Model?.Id?.ToString();
            }
        }

        private void UpdateHolderReference()
        {
            if (mat is null)
                return;

            NCardHolder? foundHolder = null;
            for (Node? curr = GetParent(); curr is not null; curr = curr.GetParent())
            {
                if (curr is NCardHolder h)
                {
                    foundHolder = h;
                    break;
                }
            }

            if (_cardHolder == foundHolder)
                return;

            _cardHolder = foundHolder;

            mat.SetShaderParameter(XRotKey, 0f);
            mat.SetShaderParameter(YRotKey, 0f);
        }

        private void CheckForIdUpdate()
        {
            if (_cardRoot is NCard nCard)
            {
                string? currentModelId = nCard.Model?.Id?.ToString();
                if (currentModelId != CardId)
                {
                    CardId = currentModelId;
                }
            }
        }

        public override void _Process(double delta)
        {
            if (mat is null || _cardRoot is null)
                return;

            CheckForIdUpdate();

            if (!string.IsNullOrEmpty(CardId))
            {
                int savedEffect = Config.GetEffect(CardId);
                if (savedEffect != _lastAppliedEffect)
                {
                    mat.SetShaderParameter(EffectModeKey, savedEffect);
                    _lastAppliedEffect = savedEffect;
                }

                double savedIntensity = Config.GetIntensity(savedEffect);
                if (savedIntensity != _lastAppliedIntensity)
                {
                    mat.SetShaderParameter(IntensityKey, savedIntensity);
                    _lastAppliedIntensity = savedIntensity;
                }
            }

            UpdateHolderReference();

            if (!IsInstanceValid(_cardHolder))
                return;

            float targetX = 0;
            float targetY = 0;

            bool hovered =
                _cardHolder is NHandCardHolder { ZIndex: > 0 }
                || (_cardHolder.Hitbox is { IsEnabled: true } && _cardHolder.Hitbox._isHovered);

            if (hovered)
            {
                Vector2 offset = _cardRoot.GetGlobalMousePosition() - _cardRoot.GlobalPosition;
                Vector2 scale = _cardRoot.GetGlobalTransform().Scale.Max(0.01f) * 256f;

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
        [HarmonyPostfix]
        public static void Postfix(NCard __instance)
        {
            if (__instance.Body is not Control body)
                return;

            if (body.GetParent()?.GetParent()?.GetParent() is not Node cardRoot)
                return;

            GpuParticles2D? glow =
                (GpuParticles2D?)__instance._rareGlow ?? __instance._uncommonGlow;

            if (GodotObject.IsInstanceValid(glow) && glow.GetParent() == body)
            {
                body.RemoveChild(glow);

                cardRoot.AddChildSafely(glow);
                cardRoot.MoveChild(glow, 0);
            }
        }
    }
}
