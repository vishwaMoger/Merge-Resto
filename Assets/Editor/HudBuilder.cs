using System.Collections.Generic;
using MergeResto.Game.UI;
using MergeResto.Game.Views;
using MergeResto.Sim;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace MergeResto.EditorTools
{
    /// <summary>
    /// Builds the styled HUD: top bar, station markers, upgrade card, welcome-back dialog,
    /// popups and flying coins, using the generated UI art and Lilita One / Fredoka fonts.
    /// </summary>
    public static class HudBuilder
    {
        static TMP_FontAsset _display, _body;
        static Material _outlined, _shadow;
        static readonly Color Brown = new(0.36f, 0.23f, 0.18f);
        static readonly Color SoftBrown = new(0.55f, 0.42f, 0.36f);

        public static void Build(StationView[] stations, CustomerCrowd crowd, out FloatingTextLayer popups, out CoinFlyLayer coinFly)
        {
            if (UiArtGenerator.Load("panel") == null)
                UiArtGenerator.GenerateAll();
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiArtGenerator.DisplayFontPath) == null)
                UiArtGenerator.GenerateFonts();
            _display = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiArtGenerator.DisplayFontPath);
            _body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiArtGenerator.BodyFontPath);
            _outlined = AssetDatabase.LoadAssetAtPath<Material>(UiArtGenerator.DisplayOutlinedPath);
            _shadow = AssetDatabase.LoadAssetAtPath<Material>(UiArtGenerator.DisplayShadowPath);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var root = (RectTransform)canvasGo.transform;

            var worldLayer = Stretch(Rect("WorldLayer", root));
            foreach (var s in stations)
                StationMarker(worldLayer, s);

            var popupLayer = Stretch(Rect("Popups", root));
            var template = Label("PopupTemplate", popupLayer, "+50", 58, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            template.rectTransform.sizeDelta = new Vector2(500, 100);
            popups = popupLayer.gameObject.AddComponent<FloatingTextLayer>();
            Set(popups, "template", template);
            Set(popups, "crowd", crowd);

            var topBar = TopBar(root, out var coinTarget, out var coinJuice);
            StationCard(root);
            OfflineDialog(root);

            var flyLayer = Stretch(Rect("CoinFly", root));
            var coin = Rect("Coin", flyLayer);
            coin.sizeDelta = new Vector2(64, 64);
            Img(coin, "icon_coin", Color.white);
            coinFly = flyLayer.gameObject.AddComponent<CoinFlyLayer>();
            Set(coinFly, "coinTemplate", coin);
            Set(coinFly, "target", coinTarget);
            Set(coinFly, "targetJuice", coinJuice);
        }

        // ------------------------------------------------------------------ Station markers

        static void StationMarker(RectTransform layer, StationView station)
        {
            var root = Rect($"Marker_{station.name}", layer);
            root.sizeDelta = new Vector2(10, 10);
            var hud = root.gameObject.AddComponent<StationHud>();
            hud.station = station;

            var ring = Rect("Ring", root);
            ring.sizeDelta = new Vector2(92, 92);
            Img(ring, "circle", new Color(0.3f, 0.18f, 0.14f, 0.55f));
            var fillRt = Rect("Fill", ring);
            fillRt.sizeDelta = new Vector2(84, 84);
            var fill = Img(fillRt, "ring", UiArtGenerator.Gold);
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            fill.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            fill.fillClockwise = true;
            var rim = Rect("Rim", ring);
            rim.sizeDelta = new Vector2(96, 96);
            Img(rim, "ring_outlined", Color.white);
            var icon = Rect("Icon", ring);
            icon.sizeDelta = new Vector2(46, 46);
            Img(icon, IconFor(station), Color.white);

            var tap = Rect("TapPrompt", root);
            tap.sizeDelta = new Vector2(150, 115);
            tap.pivot = new Vector2(0.5f, 0f);
            Img(tap, "bubble", Color.white);
            var tapLabel = Label("Label", tap, "TAP!", 44, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            var lr = tapLabel.rectTransform;
            lr.anchorMin = new Vector2(0f, 0.3f);
            lr.anchorMax = new Vector2(1f, 0.95f);
            lr.offsetMin = lr.offsetMax = Vector2.zero;

            var badge = Rect("ManagerBadge", root);
            badge.sizeDelta = new Vector2(150, 52);
            badge.anchoredPosition = new Vector2(0, -74);
            Img(badge, "button_green", Color.white);
            var hat = Rect("Hat", badge);
            hat.anchorMin = hat.anchorMax = new Vector2(0f, 0.5f);
            hat.anchoredPosition = new Vector2(26, 2);
            hat.sizeDelta = new Vector2(46, 46);
            Img(hat, "icon_manager", Color.white);
            var auto = Label("Label", badge, "AUTO", 30, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            Fill(auto.rectTransform, new Vector2(40, 4), new Vector2(-8, 0));

            var stockPill = Rect("StockPill", root);
            stockPill.sizeDelta = new Vector2(96, 46);
            stockPill.anchoredPosition = new Vector2(92, -4);
            Img(stockPill, "pill", Color.white);
            var stock = Label("Stock", stockPill, "", 32, _display, _shadow, Brown, TextAlignmentOptions.Center);
            Stretch(stock.rectTransform);
            stockPill.gameObject.AddComponent<HideWhenEmpty>().label = stock;

            Set(hud, "ring", ring.gameObject);
            Set(hud, "ringFill", fill);
            Set(hud, "tapPrompt", tap.gameObject);
            Set(hud, "managerBadge", badge.gameObject);
            Set(hud, "stockText", stock);
        }

        static string IconFor(StationView s) => s.name switch
        {
            "FlourStorage" => "icon_output",
            "Mixer" => "icon_machine",
            "Oven" => "icon_speed",
            _ => "icon_coin",
        };

        // ------------------------------------------------------------------ Top bar

        static RectTransform TopBar(RectTransform root, out RectTransform coinTarget, out ButtonJuice coinJuice)
        {
            var bar = Rect("TopBar", root);
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0f, 1f);
            bar.anchoredPosition = new Vector2(52, -26);
            bar.sizeDelta = new Vector2(720, 100);
            var view = bar.gameObject.AddComponent<TopBarView>();

            var coins = Counter(bar, "Coins", new Vector2(0, 0), 360, "icon_coin", out var coinsText, out coinTarget);
            var gems = Counter(bar, "Gems", new Vector2(400, 0), 250, "icon_gem", out var gemsText, out _);
            coinJuice = coins.gameObject.AddComponent<ButtonJuice>();
            var gemJuice = gems.gameObject.AddComponent<ButtonJuice>();
            Set(view, "coinsText", coinsText);
            Set(view, "gemsText", gemsText);
            Set(view, "coinsJuice", coinJuice);
            Set(view, "gemsJuice", gemJuice);

            // Shop name banner, top right.
            var banner = Rect("ShopBanner", root);
            banner.anchorMin = banner.anchorMax = banner.pivot = new Vector2(1f, 1f);
            banner.anchoredPosition = new Vector2(-28, -14);
            banner.sizeDelta = new Vector2(600, 150);
            Img(banner, "banner", Color.white);
            var name = Label("Name", banner, "Grandma's Bakery", 48, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            BandText(name.rectTransform, 150f);
            return bar;
        }

        static RectTransform Counter(RectTransform bar, string name, Vector2 pos, float width, string icon, out TMP_Text label, out RectTransform iconRect)
        {
            var pill = Rect(name, bar);
            pill.anchorMin = pill.anchorMax = new Vector2(0f, 1f);
            pill.pivot = new Vector2(0f, 0.5f);
            pill.anchoredPosition = pos + new Vector2(0, -50);
            pill.sizeDelta = new Vector2(width, 80);
            Img(pill, "pill_dark", Color.white);
            iconRect = Rect("Icon", pill);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-6, 2);
            iconRect.sizeDelta = new Vector2(108, 108);
            Img(iconRect, icon, Color.white);
            label = Label("Value", pill, "0", 50, _display, _outlined, Color.white, TextAlignmentOptions.Left);
            Fill(label.rectTransform, new Vector2(104, 4), new Vector2(-24, 0));
            return pill;
        }

        // ------------------------------------------------------------------ Station card

        static void StationCard(RectTransform root)
        {
            var holder = Stretch(Rect("StationPanel", root));
            var panel = holder.gameObject.AddComponent<StationPanel>();

            var sheet = Rect("Card", holder);
            sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(0f, 0f);
            sheet.anchoredPosition = new Vector2(40, 40);
            sheet.sizeDelta = new Vector2(780, 610);
            var shadow = Rect("Shadow", sheet);
            Fill(shadow, new Vector2(-24, -40), new Vector2(24, 8));
            Img(shadow, "shadow", Color.white);
            var bg = Stretch(Rect("Background", sheet));
            Img(bg, "panel", Color.white).raycastTarget = true;

            var ribbon = Rect("Ribbon", sheet);
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0, 52);
            ribbon.sizeDelta = new Vector2(640, 150);
            Img(ribbon, "banner", Color.white);
            var title = Label("Title", ribbon, "Station", 50, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            BandText(title.rectTransform, 150f);

            var info = Label("Info", sheet, "", 28, _body, null, SoftBrown, TextAlignmentOptions.Center);
            Top(info.rectTransform, new Vector2(0, -64), new Vector2(720, 40), centered: true);

            var close = Rect("Close", sheet);
            close.anchorMin = close.anchorMax = new Vector2(1f, 1f);
            close.anchoredPosition = new Vector2(-18, -18);
            close.sizeDelta = new Vector2(84, 84);
            var closeBtn = close.gameObject.AddComponent<UnityEngine.UI.Button>();
            closeBtn.targetGraphic = Img(close, "button_red", Color.white);
            var x = Rect("X", close);
            x.sizeDelta = new Vector2(50, 50);
            x.anchoredPosition = new Vector2(0, 3);
            Img(x, "icon_close", Color.white);
            close.gameObject.AddComponent<ButtonJuice>();

            var rows = new List<StationPanel.UpgradeRow>();
            (UpgradeTrack track, string icon)[] defs = { (UpgradeTrack.Speed, "icon_speed"), (UpgradeTrack.Output, "icon_output"), (UpgradeTrack.Machines, "icon_machine") };
            for (int i = 0; i < defs.Length; i++)
                rows.Add(Row(sheet, defs[i].track, defs[i].icon, -134 - i * 108, "icon_coin", "button_green"));
            var mgr = Row(sheet, UpgradeTrack.Speed, "icon_manager", -134 - 3 * 108, "icon_gem", "button_blue", manager: true);

            Set(panel, "sheet", sheet);
            Set(panel, "stationName", title);
            Set(panel, "stationInfo", info);
            SetRows(panel, rows);
            Set(panel, "managerTitle", mgr.title);
            Set(panel, "managerDetail", mgr.detail);
            Set(panel, "managerCost", mgr.cost);
            Set(panel, "managerButton", mgr.button);
            Set(panel, "managerButtonImage", mgr.buttonImage);
            Set(panel, "managerCostIcon", mgr.costIcon);
            Set(panel, "closeButton", closeBtn);
            Set(panel, "affordableSprite", UiArtGenerator.Load("button_green"));
            Set(panel, "unaffordableSprite", UiArtGenerator.Load("button_grey"));
            Set(panel, "maxedSprite", UiArtGenerator.Load("button_gold"));
            Set(panel, "managerSprite", UiArtGenerator.Load("button_blue"));
        }

        static StationPanel.UpgradeRow Row(RectTransform sheet, UpgradeTrack track, string icon, float y, string costIcon, string buttonSprite, bool manager = false)
        {
            var row = Rect(manager ? "ManagerRow" : $"Row_{track}", sheet);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0, y);
            row.sizeDelta = new Vector2(-56, 98);
            Img(row, "panel_inset", manager ? new Color(0.88f, 0.95f, 1f) : Color.white);

            var iconBg = Rect("IconBg", row);
            iconBg.anchorMin = iconBg.anchorMax = new Vector2(0f, 0.5f);
            iconBg.anchoredPosition = new Vector2(54, 0);
            iconBg.sizeDelta = new Vector2(78, 78);
            Img(iconBg, "circle", manager ? new Color(0.78f, 0.88f, 1f) : new Color(1f, 0.9f, 0.76f));
            var ic = Rect("Icon", iconBg);
            ic.sizeDelta = new Vector2(62, 62);
            Img(ic, icon, Color.white);

            var title = Label("Title", row, "Upgrade", 36, _display, _shadow, Brown, TextAlignmentOptions.Left);
            Left(title.rectTransform, new Vector2(108, 20), new Vector2(280, 46));
            var detail = Label("Detail", row, "", 26, _body, null, SoftBrown, TextAlignmentOptions.Left);
            Left(detail.rectTransform, new Vector2(108, -22), new Vector2(360, 38));

            TMP_Text level = null;
            if (!manager)
            {
                var chip = Rect("Level", row);
                chip.anchorMin = chip.anchorMax = new Vector2(0f, 0.5f);
                chip.pivot = new Vector2(0f, 0.5f);
                chip.anchoredPosition = new Vector2(392, 20);
                chip.sizeDelta = new Vector2(96, 40);
                Img(chip, "chip", new Color(1f, 0.85f, 0.6f));
                level = Label("Text", chip, "Lv 0", 24, _display, null, Brown, TextAlignmentOptions.Center);
                Stretch(level.rectTransform);
            }

            var btn = Rect("Buy", row);
            btn.anchorMin = btn.anchorMax = new Vector2(1f, 0.5f);
            btn.pivot = new Vector2(1f, 0.5f);
            btn.anchoredPosition = new Vector2(-12, 0);
            btn.sizeDelta = new Vector2(210, 82);
            var image = Img(btn, buttonSprite, Color.white);
            var button = btn.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = Color.white;
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f);
            button.colors = colors;
            btn.gameObject.AddComponent<ButtonJuice>();
            var ci = Rect("CostIcon", btn);
            ci.anchorMin = ci.anchorMax = new Vector2(0f, 0.5f);
            ci.anchoredPosition = new Vector2(40, 4);
            ci.sizeDelta = new Vector2(54, 54);
            Img(ci, costIcon, Color.white);
            var cost = Label("Cost", btn, "50", 40, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            Fill(cost.rectTransform, new Vector2(60, 6), new Vector2(-10, 0));

            return new StationPanel.UpgradeRow
            {
                track = track, title = title, level = level, detail = detail, cost = cost,
                button = button, buttonImage = image, costIcon = ci.gameObject,
            };
        }

        // ------------------------------------------------------------------ Offline dialog

        static void OfflineDialog(RectTransform root)
        {
            var holder = Stretch(Rect("OfflineDialog", root));
            var dialog = holder.gameObject.AddComponent<OfflineEarningsDialog>();
            var dim = Stretch(Rect("Dim", holder));
            Img(dim, null, new Color(0.2f, 0.1f, 0.12f, 0.5f)).raycastTarget = true;

            var card = Rect("Card", dim);
            card.sizeDelta = new Vector2(820, 560);
            var shadow = Rect("Shadow", card);
            Fill(shadow, new Vector2(-24, -40), new Vector2(24, 8));
            Img(shadow, "shadow", Color.white);
            Img(Stretch(Rect("Background", card)), "panel", Color.white);
            var ribbon = Rect("Ribbon", card);
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0, 62);
            ribbon.sizeDelta = new Vector2(700, 168);
            Img(ribbon, "banner", Color.white);
            var title = Label("Title", ribbon, "Welcome back!", 56, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            BandText(title.rectTransform, 168f);

            var clock = Rect("Clock", card);
            clock.anchorMin = clock.anchorMax = new Vector2(0.5f, 1f);
            clock.anchoredPosition = new Vector2(0, -150);
            clock.sizeDelta = new Vector2(120, 120);
            Img(clock, "icon_clock", Color.white);
            var body = Label("Body", card, "", 32, _body, null, SoftBrown, TextAlignmentOptions.Center);
            Top(body.rectTransform, new Vector2(0, -222), new Vector2(700, 90), centered: true);

            var amountRow = Rect("AmountRow", card);
            amountRow.anchorMin = amountRow.anchorMax = new Vector2(0.5f, 0f);
            amountRow.anchoredPosition = new Vector2(0, 200);
            amountRow.sizeDelta = new Vector2(500, 110);
            var coin = Rect("Coin", amountRow);
            coin.anchorMin = coin.anchorMax = new Vector2(0f, 0.5f);
            coin.anchoredPosition = new Vector2(60, 0);
            coin.sizeDelta = new Vector2(110, 110);
            Img(coin, "icon_coin", Color.white);
            var amount = Label("Amount", amountRow, "+0", 84, _display, _outlined, UiArtGenerator.Gold, TextAlignmentOptions.Left);
            Fill(amount.rectTransform, new Vector2(130, 0), Vector2.zero);

            var btn = Rect("Collect", card);
            btn.anchorMin = btn.anchorMax = new Vector2(0.5f, 0f);
            btn.anchoredPosition = new Vector2(0, 90);
            btn.sizeDelta = new Vector2(380, 116);
            var button = btn.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = Img(btn, "button_green", Color.white);
            btn.gameObject.AddComponent<ButtonJuice>();
            var label = Label("Label", btn, "Collect", 56, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            Fill(label.rectTransform, new Vector2(10, 8), new Vector2(-10, 0));

            Set(dialog, "root", dim.gameObject);
            Set(dialog, "body", body);
            Set(dialog, "amount", amount);
            Set(dialog, "collectButton", button);
        }

        // ------------------------------------------------------------------ Helpers

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static void Fill(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = min;
            rt.offsetMax = max;
        }

        static void BandText(RectTransform rt, float height)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(120f, height * 0.17f);
            rt.offsetMax = new Vector2(-120f, -height * 0.22f);
        }

        static void Top(RectTransform rt, Vector2 pos, Vector2 size, bool centered)
        {
            float x = centered ? 0.5f : 0f;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(x, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Left(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static UnityEngine.UI.Image Img(RectTransform rt, string sprite, Color color)
        {
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            var s = sprite != null ? UiArtGenerator.Load(sprite) : null;
            img.sprite = s;
            img.color = color;
            bool sliced = s != null && s.border.sqrMagnitude > 0f;
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.pixelsPerUnitMultiplier = sliced ? 1.6f : 1f;
            img.preserveAspect = !sliced && s != null;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Label(string name, Transform parent, string value, float size, TMP_FontAsset font, Material mat,
            Color color, TextAlignmentOptions align)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            if (mat != null)
                t.fontSharedMaterial = mat;
            t.text = value;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            if (font == _body)
                t.fontWeight = FontWeight.SemiBold;
            return t;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[Merge Resto] {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetRows(StationPanel panel, List<StationPanel.UpgradeRow> rows)
        {
            var so = new SerializedObject(panel);
            var prop = so.FindProperty("rows");
            prop.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                var e = prop.GetArrayElementAtIndex(i);
                var r = rows[i];
                e.FindPropertyRelative("track").enumValueIndex = (int)r.track;
                e.FindPropertyRelative("title").objectReferenceValue = r.title;
                e.FindPropertyRelative("level").objectReferenceValue = r.level;
                e.FindPropertyRelative("detail").objectReferenceValue = r.detail;
                e.FindPropertyRelative("cost").objectReferenceValue = r.cost;
                e.FindPropertyRelative("button").objectReferenceValue = r.button;
                e.FindPropertyRelative("buttonImage").objectReferenceValue = r.buttonImage;
                e.FindPropertyRelative("costIcon").objectReferenceValue = r.costIcon;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
