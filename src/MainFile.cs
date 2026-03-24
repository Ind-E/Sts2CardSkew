using Godot;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using static Godot.CanvasItem;

namespace Skew;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "Skew";
    private static SceneTree? tree;
    private static readonly Shader SkewShader = new Shader { Code = SkewShaderCode };

    private static readonly StringName PropX = "x_rot";
    private static readonly StringName PropY = "y_rot";
    private static readonly StringName PropFov = "fov";
    private static readonly StringName PropInset = "inset";

    public static void Initialize()
    {
        tree = Engine.GetMainLoop() as SceneTree;
        if (tree is not null)
        {
            tree.NodeAdded += OnNodeAdded;
        }
    }

    private static async void OnNodeAdded(Node node)
    {
        if (tree is null || node is not Control cardRoot || cardRoot.Name != "Card")
            return;

        if (!cardRoot.IsNodeReady())
            await tree.ToSignal(node, Node.SignalName.Ready);

        if (
            cardRoot.HasNode("SkewViewportContainer")
            || cardRoot.GetNodeOrNull<Control>("CardContainer") is not Control cardContainer
        )
            return;

        var size = new Vector2I(512, 512);

        var mat = new ShaderMaterial { Shader = SkewShader };
        mat.SetShaderParameter(PropX, 0f);
        mat.SetShaderParameter(PropY, 0f);
        mat.SetShaderParameter(PropFov, 90f);
        mat.SetShaderParameter(PropInset, 0f);

        var viewportContainer = new SkewContainer
        {
            Material = mat,
            Name = "SkewViewportContainer",
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            CustomMinimumSize = size,
            Size = size,
            Stretch = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = -size / 2,
            PivotOffset = size / 2,
        };
        var viewport = new SubViewport { TransparentBg = true, Size = size };

        cardContainer.Position = size / 2;

        cardRoot.RemoveChild(cardContainer);
        cardRoot.AddChild(viewportContainer);
        viewportContainer.AddChild(viewport);
        viewport.AddChild(cardContainer);
    }

    private partial class SkewContainer : SubViewportContainer
    {
        private const float MaxTilt = 16.0f;
        private const float LerpSpeed = 0.2f;

        private ShaderMaterial? mat;
        private Control? cardRoot;
        private NCardHolder? cardHolder;

        public override void _Ready()
        {
            mat = Material as ShaderMaterial;
            cardRoot = GetParent<Control>();
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

            if (cardHolder == foundHolder)
                return;

            cardHolder = foundHolder;
            mat?.SetShaderParameter(PropX, 0f);
            mat?.SetShaderParameter(PropY, 0f);
        }

        public override void _Process(double delta)
        {
            if (mat is null || cardRoot is null)
                return;

            UpdateHolderReference();

            if (!GodotObject.IsInstanceValid(cardHolder))
                return;

            float targetX = 0;
            float targetY = 0;

            bool hovered =
                cardHolder is NHandCardHolder { ZIndex: > 0 }
                || cardHolder.Hitbox is { IsEnabled: true, _isHovered: true };

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

    private const string SkewShaderCode =
        @"
shader_type canvas_item;
uniform float fov : hint_range(1, 179) = 90;
uniform bool cull_back = true;
uniform float y_rot : hint_range(-180, 180) = 0.0;
uniform float x_rot : hint_range(-180, 180) = 0.0;
uniform float inset : hint_range(0, 1) = 0.0;

varying flat vec2 o;
varying vec3 p;

void vertex(){
    float sin_b = sin(y_rot / 180.0 * PI);
    float cos_b = cos(y_rot / 180.0 * PI);
    float sin_c = sin(x_rot / 180.0 * PI);
    float cos_c = cos(x_rot / 180.0 * PI);
    mat3 inv_rot_mat;
    inv_rot_mat[0][0] = cos_b;
    inv_rot_mat[0][1] = 0.0;
    inv_rot_mat[0][2] = -sin_b;
    inv_rot_mat[1][0] = sin_b * sin_c;
    inv_rot_mat[1][1] = cos_c;
    inv_rot_mat[1][2] = cos_b * sin_c;
    inv_rot_mat[2][0] = sin_b * cos_c;
    inv_rot_mat[2][1] = -sin_c;
    inv_rot_mat[2][2] = cos_b * cos_c;
    float t = tan(fov / 360.0 * PI);
    p = inv_rot_mat * vec3((UV - 0.5), 0.5 / t);
    float v = (0.5 / t) + 0.5;
    p.xy *= v * inv_rot_mat[2].z;
    o = v * inv_rot_mat[2].xy;
    VERTEX += (UV - 0.5) / TEXTURE_PIXEL_SIZE * t * (1.0 - inset);
}

void fragment(){
    if (cull_back && p.z <= 0.0) discard;
    vec2 uv = (p.xy / p.z).xy - o;
    COLOR = texture(TEXTURE, uv + 0.5);
    COLOR.a *= step(max(abs(uv.x), abs(uv.y)), 0.5);
}";
}
