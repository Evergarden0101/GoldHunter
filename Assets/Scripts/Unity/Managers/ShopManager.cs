using System.Collections.Generic;
using GoldHunter.Core.Config;
using GoldHunter.Core.Events;
using GoldHunter.Core.Services;
using GoldHunter.Core.Simulation;
using UnityEngine;

namespace GoldHunter.Unity.Managers
{
    /// <summary>
    /// The shopping front end.
    ///
    /// Pricing and funding rules belong to the core's
    /// <see cref="ShoppingService"/>; this component owns the scene side —
    /// spawning the stalls, tracking who is browsing, and drawing each
    /// customer's panel.
    ///
    /// Purchases bill the bag first and the vault for the remainder, so an
    /// upgrade always costs final score. Prices that need the vault are shown
    /// in amber to make that visible before you commit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopManager : MonoBehaviour
    {
        [Header("Presentation")]
        [Tooltip("Optional prefab for a shop stall. A simple marker is generated when empty.")]
        [SerializeField] private GameObject _shopPrefab;
        [SerializeField] private Color _stallColor = new Color(0.55f, 0.35f, 0.75f);

        [Header("Panel")]
        [SerializeField] private bool _drawPanels = true;
        [SerializeField] private int _panelWidth = 260;
        [SerializeField] private Color _affordableColor = new Color(1f, 0.79f, 0.22f);
        [SerializeField] private Color _needsVaultColor = new Color(1f, 0.65f, 0.32f);
        [SerializeField] private Color _unaffordableColor = new Color(0.71f, 0.38f, 0.25f);

        private ShoppingService _shopping;
        private MatchSimulation _sim;
        private StageManager _stage;
        private readonly List<ShopRow> _rows = new List<ShopRow>();
        private GUIStyle _rowStyle;
        private GUIStyle _headerStyle;

        public ShoppingService Service => _shopping;

        internal void Bind(MatchSimulation sim, StageManager stage)
        {
            _sim = sim;
            _shopping = sim.Shopping;
            _stage = stage;
            BuildViews();
        }

        private void BuildViews()
        {
            for (int i = 0; i < _sim.Shops.Count; i++)
            {
                Shop shop = _sim.Shops[i];

                GameObject go = _shopPrefab != null
                    ? Instantiate(_shopPrefab, transform)
                    : GameObject.CreatePrimitive(PrimitiveType.Cube);

                go.name = $"Shop_{shop.Id}";
                go.transform.SetParent(transform, false);
                go.transform.position = _stage.ToWorld(shop.Position, shop.Radius * 0.5f);
                go.transform.localScale = new Vector3(shop.Radius * 1.6f, shop.Radius, shop.Radius * 1.6f);

                var renderer = go.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = _stallColor;
            }
        }

        /* ------------------------------------------------------------ queries */

        /// <summary>Everything a player could spend right now: bag plus vault.</summary>
        public float FundsOf(PlayerState player) => _shopping != null ? _shopping.Funds(player) : 0f;

        public int PriceOf(PlayerState player, ItemId item) =>
            _shopping != null ? _shopping.PriceOf(player, item) : 0;

        public bool CanBuy(PlayerState player, ItemId item) =>
            _shopping != null && _shopping.CanBuy(player, item);

        /// <summary>
        /// Buys on behalf of a player. Routed through the simulation so the
        /// rejection reasons reach the same listeners as an in-game purchase.
        /// </summary>
        public bool TryBuy(PlayerState player, ItemId item) => _sim != null && _sim.TryBuy(player, item);

        internal void OnPurchase(in PurchaseEvent evt) { }

        /* -------------------------------------------------------------- panel */

        private void OnGUI()
        {
            if (!_drawPanels || _sim == null || _shopping == null) return;

            EnsureStyles();

            int slot = 0;
            for (int i = 0; i < _sim.Players.Count; i++)
            {
                PlayerState player = _sim.Players[i];
                if (player.CurrentShop == null) continue;

                DrawPanel(player, slot++);
            }
        }

        private void DrawPanel(PlayerState player, int slot)
        {
            _shopping.BuildRows(player, _rows);

            const int rowHeight = 22;
            int height = rowHeight * (_rows.Count + 2) + 16;
            int x = 12 + slot * (_panelWidth + 12);
            int y = Screen.height - height - 12;

            GUI.Box(new Rect(x, y, _panelWidth, height), GUIContent.none);

            float vault = player.Home != null ? player.Home.Vault : 0f;
            GUI.Label(new Rect(x + 10, y + 6, _panelWidth - 20, 20),
                $"{player.Name} — {Mathf.Floor(player.Bag)}g bag + {Mathf.Floor(vault)}g vault",
                _headerStyle);

            for (int i = 0; i < _rows.Count; i++)
            {
                ShopRow row = _rows[i];
                var rect = new Rect(x + 10, y + 28 + i * rowHeight, _panelWidth - 20, rowHeight);

                if (i == player.ShopSelection)
                {
                    GUI.Box(rect, GUIContent.none);
                    if (player.BuyHold > 0f)
                    {
                        float progress = Mathf.Clamp01(player.BuyHold / _sim.Config.Shop.BuyHoldSeconds);
                        var fill = new Rect(rect.x, rect.y, rect.width * progress, rect.height);
                        Color previous = GUI.color;
                        GUI.color = new Color(1f, 0.79f, 0.22f, 0.35f);
                        GUI.Box(fill, GUIContent.none);
                        GUI.color = previous;
                    }
                }

                _rowStyle.normal.textColor = row.IsMaxed ? Color.grey
                    : !row.IsAffordable ? _unaffordableColor
                    : row.NeedsVault ? _needsVaultColor
                    : _affordableColor;

                string level = row.Item.MaxLevel > 1
                    ? $"{row.Level}/{row.Item.MaxLevel}"
                    : (row.Level > 0 ? "owned" : "");
                string price = row.IsMaxed ? "MAX" : $"{row.Price}g";

                GUI.Label(rect, $"{row.Item.DisplayName}   {level}   {price}", _rowStyle);
            }

            GUI.Label(new Rect(x + 10, y + height - 22, _panelWidth - 20, 20),
                player.IsHuman ? "Dash = next   ·   hold Punch = buy" : "shopping…", _rowStyle);
        }

        private void EnsureStyles()
        {
            if (_rowStyle != null) return;
            _rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            _headerStyle.normal.textColor = _affordableColor;
        }
    }
}
