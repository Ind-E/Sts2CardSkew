using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace BalatroEffects;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "BalatroEffects";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        new Harmony(ModId).PatchAll();
        Config.Load();

        if (Engine.GetMainLoop() is SceneTree tree)
        {
            tree.NodeAdded += OnNodeAdded;
        }
    }

    private static readonly StringName InspectCardScreen = "InspectCardScreen";
    private static readonly StringName Card = "Card";

    private static void OnNodeAdded(Node node)
    {
        if (node is not Control control)
            return;

        switch (control)
        {
            case NCard card when card.Name == Card:
                card.OnReady(() => ShaderController.ApplyShader(card));
                break;

            case Control c when c.Name == InspectCardScreen:
                c.OnReady(() => InspectScreen.AddButtons(c));
                break;
        }
    }
}

public static class NodeExtensions
{
    public static void OnReady(this Node node, Action action)
    {
        if (node.IsNodeReady())
            action();
        else
            node.Ready += action;
    }
}
