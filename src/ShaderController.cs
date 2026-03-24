using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using static Godot.CanvasItem;

namespace BalatroEffects;

public partial class ShaderController
{
    private static readonly StringName PropX = "x_rot";
    private static readonly StringName PropY = "y_rot";
    private static readonly StringName PropFov = "fov";
    private static readonly StringName PropInset = "inset";
    private static readonly StringName PropEffectMode = "effect_mode";

    private static readonly Shader EffectsShader = new Shader { Code = ShaderCode.Code };

    public static void ApplyShader(NCard cardRoot)
    {
        if (
            cardRoot.HasNode("BalatroShaderViewportContainer")
            || cardRoot.GetNodeOrNull<Control>("CardContainer") is not Control cardContainer
            || cardRoot?.Model?.Id?.ToString() is not string cardId
        )
            return;

        var size = new Vector2I(512, 512);

        var mat = new ShaderMaterial { Shader = EffectsShader };
        mat.SetShaderParameter(PropX, 0f);
        mat.SetShaderParameter(PropY, 0f);
        mat.SetShaderParameter(PropFov, 90f);
        mat.SetShaderParameter(PropInset, 0f);

        var viewportContainer = new ShaderContainer
        {
            Material = (ShaderMaterial)mat.Duplicate(),
            Name = "BalatroShaderViewportContainer",
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            CustomMinimumSize = size,
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

        int savedIndex = Config.GetIndex(cardId);
        mat.SetShaderParameter(PropEffectMode, savedIndex);
    }

    private partial class ShaderContainer : SubViewportContainer
    {
        private const float MaxTilt = 16.0f;
        private const float LerpSpeed = 0.2f;

        private Control? cardRoot;
        private NCardHolder? cardHolder;
        private int lastAppliedIndex = -1;

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

            mat.SetShaderParameter(PropX, 0f);
            mat.SetShaderParameter(PropY, 0f);
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

        static AccessTools.FieldRef<NClickableControl, bool> IsHovered = AccessTools.FieldRefAccess<
            NClickableControl,
            bool
        >("_isHovered");

        public override void _Process(double delta)
        {
            if (mat is null || cardRoot is null)
                return;

            CheckForIdUpdate();

            if (!string.IsNullOrEmpty(CardId))
            {
                int targetIndex = Config.GetIndex(CardId);
                if (targetIndex != lastAppliedIndex)
                {
                    lastAppliedIndex = targetIndex;
                    mat.SetShaderParameter(PropEffectMode, targetIndex);
                }
            }

            UpdateHolderReference();

            if (!GodotObject.IsInstanceValid(cardHolder))
                return;

            float targetX = 0;
            float targetY = 0;

            bool hovered =
                cardHolder is NHandCardHolder { ZIndex: > 0 }
                || cardHolder.Hitbox is { IsEnabled: true } && IsHovered(cardHolder.Hitbox);

            if (hovered)
            {
                Vector2 offset = cardRoot.GetGlobalMousePosition() - cardRoot.GlobalPosition;
                Vector2 scale = cardRoot.GetGlobalTransform().Scale.Max(0.01f) * 256f;

                targetX = (offset.Y / scale.X) * -MaxTilt;
                targetY = (offset.X / scale.Y) * MaxTilt;
            }

            targetX = Mathf.Clamp(targetX, -MaxTilt, MaxTilt);
            targetY = Mathf.Clamp(targetY, -MaxTilt, MaxTilt);

            float curX = (float)mat.GetShaderParameter("x_rot");
            float curY = (float)mat.GetShaderParameter("y_rot");

            mat.SetShaderParameter("x_rot", Mathf.Lerp(curX, targetX, LerpSpeed));
            mat.SetShaderParameter("y_rot", Mathf.Lerp(curY, targetY, LerpSpeed));
        }
    }
}
