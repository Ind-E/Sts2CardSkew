using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace BalatroEffects;

public partial class InspectScreen
{
    public static void AddButtons(Control root)
    {
        if (
            root.FindChild("Upgrade") is not NUpgradePreviewTickbox upgrade
            || root.HasNode("BalatroEffectsPaginator")
        )
            return;

        var paginatorScene = GD.Load<PackedScene>("res://scenes/screens/paginator.tscn");
        var paginator = (Control)paginatorScene.Instantiate();
        var fxpaginator = new FXPaginator { Name = "BalatroEffectsPaginator" };

        foreach (Node child in paginator.GetChildren().ToArray())
        {
            paginator.RemoveChild(child);
            fxpaginator.AddChild(child);
        }

        var hbox = new HBoxContainer
        {
            Name = "BalatroEffectsHBox",
            Alignment = BoxContainer.AlignmentMode.End,
        };
        hbox.AddChild(fxpaginator);
        hbox.Position = new Vector2(1920 - fxpaginator.Size.X - 200, upgrade.Position.Y);
        root.AddChild(hbox);
    }

    public partial class FXPaginator : NPaginator
    {
        private string cardId = "UnknownCard";

        public override void _Ready()
        {
            _label = (MegaLabel)FindChild("Label");
            ref MegaLabel vfxField = ref AccessTools.FieldRefAccess<NPaginator, MegaLabel>(
                "_vfxLabel"
            )(this);
            vfxField = (MegaLabel)FindChild("VfxLabel");

            _options.AddRange(new[] { "None", "Foil", "Negative", "Polychrome", "Holographic" });

            _currentIndex = Config.GetIndex(cardId);
            _currentIndex = Mathf.Clamp(_currentIndex, 0, _options.Count - 1);
            _label.SetTextAutoSize(_options[_currentIndex]);

            var leftArrow = GetNode<Control>("LeftArrow");
            var rightArrow = GetNode<Control>("RightArrow");

            leftArrow.Position = new Vector2(
                Position.X - 100 - leftArrow.CustomMinimumSize.X,
                leftArrow.Position.Y
            );
            rightArrow.Position = new Vector2(Position.X + 100 + Size.X, rightArrow.Position.Y);
        }

        public void UpdateTargetCard(string newCardId)
        {
            cardId = newCardId;

            int savedIndex = Config.GetIndex(cardId);
            _currentIndex = Mathf.Clamp(savedIndex, 0, _options.Count - 1);

            if (_label != null)
            {
                _label.SetTextAutoSize(_options[_currentIndex]);
            }
            OnIndexChanged(_currentIndex);
        }

        private static AccessTools.FieldRef<NInspectCardScreen, NCard> CardFieldRef =
            AccessTools.FieldRefAccess<NInspectCardScreen, NCard>("_card");

        protected override void OnIndexChanged(int index)
        {
            _currentIndex = index;
            _label.SetTextAutoSize(_options[index]);
            Config.SetIndex(cardId, index);
        }
    }

    private static AccessTools.FieldRef<NInspectCardScreen, NCard> CardFieldRef =
        AccessTools.FieldRefAccess<NInspectCardScreen, NCard>("_card");

    [HarmonyPatch(typeof(NInspectCardScreen), "SetCard")]
    public static class SetCardPatch
    {
        public static void Postfix(
            NInspectCardScreen __instance,
            int index,
            List<CardModel> ____cards
        )
        {
            if (____cards is null || index < 0 || index >= ____cards.Count)
                return;

            var cardNode = CardFieldRef(__instance);
            ShaderController.ApplyShader(cardNode);

            var paginator = __instance.GetNodeOrNull<FXPaginator>(
                "BalatroEffectsHBox/BalatroEffectsPaginator"
            );

            if (paginator != null)
            {
                string cardId = ____cards[index].Id.ToString();

                paginator.UpdateTargetCard(cardId);
            }
        }
    }
}
