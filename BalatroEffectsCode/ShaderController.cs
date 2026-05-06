using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
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

    private const string _viewportContainerName = "BalatroShaderViewportContainer";

    public static void ApplyShader(NCard cardRoot)
    {
        if (
            cardRoot.GetNodeOrNull<Control>("CardContainer") is not Control cardContainer
            || cardContainer.HasNode(_viewportContainerName)
            || cardRoot?.Model?.Id?.ToString() is not string cardId
        )
        {
            return;
        }

        var size = new Vector2I(480, 480);

        var fxMat = new ShaderMaterial { Shader = EffectsShader };
        float seed = cardRoot.GetHashCode() % 10000 / 10.0f;
        fxMat.SetShaderParameter(_seedKey, seed);

        int savedEffect = Config.GetEffect(cardId);
        fxMat.SetShaderParameter(_effectModeKey, savedEffect);
        fxMat.SetShaderParameter(_intensityKey, Config.GetIntensity(savedEffect));

        var fxContainer = new ShaderContainer
        {
            Material = fxMat,
            Name = _viewportContainerName,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Size = size,
            Stretch = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = -size / 2,
            PivotOffset = size / 2,
            CardId = cardId,
        };

        var fxViewport = new SubViewport { TransparentBg = true, Disable3D = true };
        var fxRoot = new Control() { Position = size / 2 };

        var tiltMat = new ShaderMaterial { Shader = EffectsShader };
        var tiltContainer = new ShaderContainer
        {
            Material = tiltMat,
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            Size = size,
            Stretch = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = -size / 2,
            PivotOffset = size / 2,
            TiltOnly = true,
        };

        var tiltViewport = new SubViewport { TransparentBg = true, Disable3D = true };
        var tiltRoot = new Control() { Position = size / 2 };

        var shadow = cardContainer.GetNodeOrNull<TextureRect>("Shadow");
        var highlight = cardContainer.GetNodeOrNull<NCardHighlight>("Highlight");

        foreach (Node child in cardContainer.GetChildren())
        {
            cardContainer.RemoveChild(child);
            if (child == shadow || child == highlight)
            {
                tiltRoot.AddChild(child);
            }
            else
            {
                fxRoot.AddChild(child);
            }
        }

        tiltViewport.AddChild(tiltRoot);
        tiltContainer.AddChild(tiltViewport);
        cardContainer.AddChild(tiltContainer);

        fxViewport.AddChild(fxRoot);
        fxContainer.AddChild(fxViewport);
        cardContainer.AddChild(fxContainer);
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

        public bool TiltOnly;

        public override void _Ready()
        {
            _cardRoot = GetParent<Control>().GetParent<Control>();
            mat = Material as ShaderMaterial;

            if (string.IsNullOrEmpty(CardId) && _cardRoot is NCard card)
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

            if (_cardHolder == foundHolder || mat is null)
                return;

            _cardHolder = foundHolder;

            mat.SetShaderParameter(_xRotKey, 0f);
            mat.SetShaderParameter(_yRotKey, 0f);
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

            if (!string.IsNullOrEmpty(CardId) && !TiltOnly)
            {
                int savedEffect = Config.GetEffect(CardId);
                if (savedEffect != _lastAppliedEffect)
                {
                    mat.SetShaderParameter(_effectModeKey, savedEffect);
                    _lastAppliedEffect = savedEffect;
                }

                double savedIntensity = Config.GetIntensity(savedEffect);
                if (savedIntensity != _lastAppliedIntensity)
                {
                    mat.SetShaderParameter(_intensityKey, savedIntensity);
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
                || (_cardHolder.Hitbox is { IsEnabled: true } hb && hb._isHovered);

            if (hovered)
            {
                Vector2 offset = _cardRoot.GetGlobalMousePosition() - _cardRoot.GlobalPosition;
                Vector2 scale = _cardRoot.GetGlobalTransform().Scale.Max(0.01f) * 256f;

                targetX = offset.Y / scale.X * -MaxTilt;
                targetY = offset.X / scale.Y * MaxTilt;
            }

            targetX = Mathf.Clamp(targetX, -MaxTilt, MaxTilt);
            targetY = Mathf.Clamp(targetY, -MaxTilt, MaxTilt);

            float curX = (float)mat.GetShaderParameter(_xRotKey);
            float curY = (float)mat.GetShaderParameter(_yRotKey);

            mat.SetShaderParameter(_xRotKey, Mathf.Lerp(curX, targetX, LerpSpeed));
            mat.SetShaderParameter(_yRotKey, Mathf.Lerp(curY, targetY, LerpSpeed));
        }
    }
}
