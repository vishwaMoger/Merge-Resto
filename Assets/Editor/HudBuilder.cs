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
    /// popups and flying coins. Surfaces and buttons come from the Sweet Land GUI pack
    /// (Persefida, Asset Store); game-specific icons (bread, chef hat, clock, gem, coin) from
    /// the generated UI art. Fonts: Lilita One / Fredoka.
    /// </summary>
    public static class HudBuilder
    {
        static TMP_FontAsset _display, _body;
        /// <summary>Shown on the banner; set by the scene builder before Build.</summary>
        public static string ShopName = "Grandma's Bakery";
        static RectTransform _root;
        static StationPanel _panel;
        static readonly List<StationHud> _markers = new();
        static readonly Color PinOrange = new(1f, 0.52f, 0.18f);
        static Material _outlined, _shadow;
        static readonly Color Brown = new(0.27f, 0.16f, 0.11f);
        static readonly Color SoftBrown = new(0.4f, 0.27f, 0.2f);
        static readonly Color FrameBrown = new(0.66f, 0.42f, 0.27f);

        const string SweetLand = "Assets/Art/UI/SweetLand/Sprites/";

        // Sweet Land sprite keys ("sl_*") resolve to the pack; anything else to the generated art.
        static Sprite Resolve(string key)
        {
            string path = key switch
            {
                "sl_btn_orange" => "Buttons/250px/Button_Square_Orange_250px",
                "sl_btn_white" => "Buttons/250px/Button_Square_White_250px",
                "sl_btn_yellow" => "Buttons/250px/Button_Square_Yellow_250px",
                "sl_btn_pink" => "Buttons/250px/Button_Square_Pink_250px",
                "sl_btn_red_circle" => "Buttons/250px/Button_Circle_Red_250px",
                "sl_btn_white_circle" => "Buttons/250px/Button_Circle_White_250px",
                "sl_banner" => "Buttons/250px/Button_Honeycomb_Orange_250px",
                "sl_frame_white" => "Frames/250px/Frame_Square_White_250px",
                "sl_frame_cream" => "Frames/250px/Frame_Square_Yellow_250px",
                "sl_frame_peach" => "Frames/250px/Frame_Square_Orange_250px",
                "sl_disc_peach" => "Frames/250px/Frame_Circle_Orange_250px",
                "sl_disc_pink" => "Frames/250px/Frame_Circle_Pink_250px",
                "sl_icon_close" => "Icons/Icon_Close",
                "sl_icon_speed" => "Joysticks/PowerUp_Ready_Cookie",
                "sl_shine" => "BG_Shining",
                "sl_btn_orange_circle" => "Buttons/250px/Button_Circle_Orange_250px",
                "sl_play" => "Icons/Icon_Play",
                "sl_green_disc" => "Scales/Scale_Honey_Green_60px",
                "sl_ping" => "Scales/Empty_Circle_250px",
                "sl_medal" => "Icons/Medal",
                "sl_disc_honey" => "Frames/250px/Frame_Circle_Honey_250px",
                "sl_disc_white" => "Frames/250px/Frame_Circle_White_250px",
                "sl_heart" => "Icons/Heart",
                "sl_music_on" => "Icons/Icon_Music_On",
                "sl_music_off" => "Icons/Icon_Music_Off",
                "sl_frame_honey" => "Frames/250px/Frame_Square_Honey_250px",
                _ => null,
            };
            return path != null ? AssetDatabase.LoadAssetAtPath<Sprite>(SweetLand + path + ".png") : UiArtGenerator.Load(key);
        }

        // Honeycomb banners slice so the pointed ends keep their shape at any height.
        static float BannerSlice(float height) => 204f / height;

        // A window: brown frame with a cream inset, like the Sweet Land windows.
        // Slice density keeps the corners from overlapping on small pills.
        static UnityEngine.UI.Image Window(RectTransform rt, float edge = 14f, float slice = 0.8f)
        {
            var frame = Img(Stretch(Rect("Frame", rt)), "sl_frame_white", FrameBrown, slice);
            var inner = Rect("Inner", rt);
            Fill(inner, new Vector2(edge, edge), new Vector2(-edge, -edge));
            Img(inner, "sl_frame_cream", Color.white, slice * 1.15f);
            return frame;
        }

        public static void Build(StationView[] stations, CustomerCrowd crowd, out FloatingTextLayer popups, out CoinFlyLayer coinFly)
        {
            if (UiArtGenerator.Load("panel") == null)
                UiArtGenerator.GenerateAll();
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiArtGenerator.DisplayFontPath) == null)
                UiArtGenerator.GenerateFonts();
            _display = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiArtGenerator.DisplayFontPath);
            _body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiArtGenerator.BodyFontPath);
            // Body text is set in bold; keep the synthetic bold from spreading letters apart.
            _body.boldStyle = 0.7f;
            _body.boldSpacing = 1;
            EditorUtility.SetDirty(_body);
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
            _root = root;
            _markers.Clear();

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
            var goals = Goals(root);
            _panel = StationCard(root);
            OfflineDialog(root);

            var flyLayer = Stretch(Rect("CoinFly", root));
            var coin = Rect("Coin", flyLayer);
            coin.sizeDelta = new Vector2(64, 64);
            Img(coin, "icon_coin", Color.white);
            coinFly = flyLayer.gameObject.AddComponent<CoinFlyLayer>();
            Set(coinFly, "coinTemplate", coin);
            Set(coinFly, "target", coinTarget);
            Set(coinFly, "targetJuice", coinJuice);
            Set(goals, "coinFly", coinFly);
        }

        // ------------------------------------------------------------------ Map

        /// <summary>"Main Street" shop map: a Shops button (bottom right) and a card per shop.</summary>
        public static void BuildMap(MapPanel.Shop[] shops)
        {
            var holder = Stretch(Rect("Map", _root));
            var board = _root.Find("Board");
            if (board != null)
                holder.SetSiblingIndex(board.GetSiblingIndex() + 1);
            var map = holder.gameObject.AddComponent<MapPanel>();

            // Shops button, left of Renovate.
            var open = Rect("ShopsButton", holder);
            open.anchorMin = open.anchorMax = open.pivot = new Vector2(1f, 0f);
            open.anchoredPosition = new Vector2(-206, 44);
            open.sizeDelta = new Vector2(150, 150);
            var openImage = Img(open, "sl_btn_orange_circle", Color.white, simple: true);
            var openButton = open.gameObject.AddComponent<UnityEngine.UI.Button>();
            openButton.targetGraphic = openImage;
            open.gameObject.AddComponent<ButtonJuice>();
            var openArt = Rect("Icon", open);
            openArt.anchoredPosition = new Vector2(0, 14);
            openArt.sizeDelta = new Vector2(108, 108);
            var openArtImage = openArt.gameObject.AddComponent<UnityEngine.UI.Image>();
            openArtImage.sprite = shops[0].picture;
            openArtImage.preserveAspect = true;
            openArtImage.raycastTarget = false;
            var openLabel = Label("Label", open, "Shops", 30, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            openLabel.rectTransform.anchorMin = openLabel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            openLabel.rectTransform.anchoredPosition = new Vector2(0, 8);
            openLabel.rectTransform.sizeDelta = new Vector2(200, 44);
            var alert = Rect("Alert", open);
            alert.anchorMin = alert.anchorMax = new Vector2(1f, 1f);
            alert.anchoredPosition = new Vector2(-18, -18);
            alert.sizeDelta = new Vector2(48, 48);
            Img(alert, "sl_btn_red_circle", Color.white, simple: true);
            var bang = Label("Text", alert, "!", 34, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            Fill(bang.rectTransform, new Vector2(0, 4), Vector2.zero);

            var dim = Stretch(Rect("Dim", holder));
            var dimImage = Img(dim, null, new Color(0.03f, 0.02f, 0.02f, 0.55f));
            var dimButton = dim.gameObject.AddComponent<UnityEngine.UI.Button>();
            dimButton.transition = UnityEngine.UI.Selectable.Transition.None;
            dimButton.targetGraphic = dimImage;
            var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();

            var panel = Rect("Panel", holder);
            panel.sizeDelta = new Vector2(1560, 720);
            panel.anchoredPosition = new Vector2(0, -10);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            Window(Stretch(Rect("Background", panel)), 16f).raycastTarget = true;
            var ribbon = Rect("Ribbon", panel);
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0, 12);
            ribbon.sizeDelta = new Vector2(480, 108);
            Img(ribbon, "sl_banner", Color.white, BannerSlice(108f));
            var title = Label("Title", ribbon, "Main Street", 52, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            BandText(title.rectTransform, 108f);
            var closeButton = CloseButton(panel);

            var cards = new List<MapPanel.Card>();
            const float cardW = 350f, gap = 20f;
            float startX = -(shops.Length * cardW + (shops.Length - 1) * gap) / 2f + cardW / 2f;
            for (int i = 0; i < shops.Length; i++)
            {
                var card = Rect($"Shop{i}", panel);
                card.anchoredPosition = new Vector2(startX + i * (cardW + gap), -34);
                card.sizeDelta = new Vector2(cardW, 560);
                Img(card, "sl_frame_white", Color.white, 1.3f);

                var pic = Rect("Picture", card);
                pic.anchorMin = pic.anchorMax = new Vector2(0.5f, 1f);
                pic.pivot = new Vector2(0.5f, 1f);
                pic.anchoredPosition = new Vector2(0, -14);
                pic.sizeDelta = new Vector2(250, 250);
                var picImage = pic.gameObject.AddComponent<UnityEngine.UI.Image>();
                picImage.preserveAspect = true;
                picImage.raycastTarget = false;
                var lockIcon = Rect("Lock", card);
                lockIcon.anchorMin = lockIcon.anchorMax = new Vector2(0.5f, 1f);
                lockIcon.anchoredPosition = new Vector2(0, -150);
                lockIcon.sizeDelta = new Vector2(90, 90);
                Img(lockIcon, "sl_btn_white_circle", new Color(0.45f, 0.32f, 0.26f), simple: true);
                var lockText = Label("Text", lockIcon, "?", 52, _display, _outlined, Color.white, TextAlignmentOptions.Center);
                Fill(lockText.rectTransform, new Vector2(0, 6), Vector2.zero);
                var here = Rect("Here", card);
                here.anchorMin = here.anchorMax = new Vector2(0.5f, 1f);
                here.anchoredPosition = new Vector2(0, -12);
                here.sizeDelta = new Vector2(220, 52);
                Img(here, "sl_btn_pink", Color.white, 2.6f);
                var hereText = Label("Text", here, "You are here", 26, _display, _outlined, Color.white, TextAlignmentOptions.Center);
                Fill(hereText.rectTransform, new Vector2(4, 4), Vector2.zero);

                var name = Label("Name", card, "Shop", 34, _display, _shadow, Brown, TextAlignmentOptions.Center);
                Top(name.rectTransform, new Vector2(0, -270), new Vector2(330, 46), centered: true);
                var blurb = Label("Blurb", card, "", 24, _body, null, SoftBrown, TextAlignmentOptions.Center);
                blurb.textWrappingMode = TextWrappingModes.Normal;
                Top(blurb.rectTransform, new Vector2(0, -318), new Vector2(310, 64), centered: true);
                var status = Label("Status", card, "", 25, _body, null, Brown, TextAlignmentOptions.Center);
                status.textWrappingMode = TextWrappingModes.Normal;
                Top(status.rectTransform, new Vector2(0, -390), new Vector2(310, 66), centered: true);

                var btn = Rect("Action", card);
                btn.anchorMin = btn.anchorMax = new Vector2(0.5f, 0f);
                btn.pivot = new Vector2(0.5f, 0f);
                btn.anchoredPosition = new Vector2(0, 20);
                btn.sizeDelta = new Vector2(250, 70);
                var btnImage = Img(btn, "sl_btn_orange", Color.white, 1.8f);
                var button = btn.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = btnImage;
                var colors = button.colors;
                colors.disabledColor = Color.white;
                button.colors = colors;
                btn.gameObject.AddComponent<ButtonJuice>();
                var btnText = Label("Text", btn, "Go", 34, _display, _shadow, Color.white, TextAlignmentOptions.Center);
                Fill(btnText.rectTransform, new Vector2(4, 4), Vector2.zero);

                cards.Add(new MapPanel.Card
                {
                    root = card, picture = picImage, title = name, blurb = blurb, status = status,
                    action = button, actionImage = btnImage, actionText = btnText,
                    lockIcon = lockIcon.gameObject, hereBadge = here.gameObject,
                });
            }

            var so = new SerializedObject(map);
            var shopsProp = so.FindProperty("shops");
            shopsProp.arraySize = shops.Length;
            for (int i = 0; i < shops.Length; i++)
            {
                var e = shopsProp.GetArrayElementAtIndex(i);
                var sh = shops[i];
                e.FindPropertyRelative("id").stringValue = sh.id;
                e.FindPropertyRelative("displayName").stringValue = sh.displayName;
                e.FindPropertyRelative("blurb").stringValue = sh.blurb;
                e.FindPropertyRelative("sceneName").stringValue = sh.sceneName ?? "";
                e.FindPropertyRelative("picture").objectReferenceValue = sh.picture;
                e.FindPropertyRelative("comingSoon").boolValue = sh.comingSoon;
                e.FindPropertyRelative("requiresShop").stringValue = sh.requiresShop ?? "";
                e.FindPropertyRelative("renovationsNeeded").intValue = sh.renovationsNeeded;
                e.FindPropertyRelative("shopLevelNeeded").intValue = sh.shopLevelNeeded;
                e.FindPropertyRelative("unlockCost").doubleValue = sh.unlockCost;
            }
            var cardsProp = so.FindProperty("cards");
            cardsProp.arraySize = cards.Count;
            for (int i = 0; i < cards.Count; i++)
            {
                var e = cardsProp.GetArrayElementAtIndex(i);
                var c = cards[i];
                e.FindPropertyRelative("root").objectReferenceValue = c.root;
                e.FindPropertyRelative("picture").objectReferenceValue = c.picture;
                e.FindPropertyRelative("title").objectReferenceValue = c.title;
                e.FindPropertyRelative("blurb").objectReferenceValue = c.blurb;
                e.FindPropertyRelative("status").objectReferenceValue = c.status;
                e.FindPropertyRelative("action").objectReferenceValue = c.action;
                e.FindPropertyRelative("actionImage").objectReferenceValue = c.actionImage;
                e.FindPropertyRelative("actionText").objectReferenceValue = c.actionText;
                e.FindPropertyRelative("lockIcon").objectReferenceValue = c.lockIcon;
                e.FindPropertyRelative("hereBadge").objectReferenceValue = c.hereBadge;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Set(map, "panel", panel);
            Set(map, "group", group);
            Set(map, "dim", dimGroup);
            Set(map, "dimButton", dimButton);
            Set(map, "openButton", openButton);
            Set(map, "openAlert", alert.gameObject);
            Set(map, "closeButton", closeButton);
            Set(map, "goSprite", Resolve("sl_btn_orange"));
            Set(map, "lockedSprite", Resolve("sl_btn_white"));
        }

        // ------------------------------------------------------------------ Sound toggle

        /// <summary>Round mute button under the shop banner.</summary>
        public static void BuildSoundToggle()
        {
            var rt = Rect("SoundToggle", _root);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-40, -150);
            rt.sizeDelta = new Vector2(76, 76);
            var bg = Img(rt, "sl_btn_orange_circle", Color.white, simple: true);
            bg.raycastTarget = true;
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = bg;
            rt.gameObject.AddComponent<ButtonJuice>();
            var icon = Rect("Icon", rt);
            icon.sizeDelta = new Vector2(46, 46);
            icon.anchoredPosition = new Vector2(0, 3);
            var iconImage = Img(icon, "sl_music_on", Color.white);
            var toggle = rt.gameObject.AddComponent<SoundToggle>();
            Set(toggle, "button", button);
            Set(toggle, "icon", iconImage);
            Set(toggle, "onSprite", Resolve("sl_music_on"));
            Set(toggle, "offSprite", Resolve("sl_music_off"));
        }

        // ------------------------------------------------------------------ Goals

        static GoalsPanel Goals(RectTransform root)
        {
            var holder = Rect("Goals", root);
            holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(0f, 1f);
            holder.anchoredPosition = new Vector2(30, -134);
            holder.sizeDelta = new Vector2(480, 320);
            var group = holder.gameObject.AddComponent<CanvasGroup>();
            var panel = holder.gameObject.AddComponent<GoalsPanel>();

            var cards = new List<GoalsPanel.Card>();
            for (int i = 0; i < 3; i++)
            {
                var card = Rect($"Goal{i}", holder);
                card.anchorMin = card.anchorMax = card.pivot = new Vector2(0f, 1f);
                card.anchoredPosition = new Vector2(0, -i * 102);
                card.sizeDelta = new Vector2(480, 94);
                Window(card, 6f, 1.4f);

                var disc = Rect("IconBg", card);
                disc.anchorMin = disc.anchorMax = new Vector2(0f, 0.5f);
                disc.anchoredPosition = new Vector2(46, 0);
                disc.sizeDelta = new Vector2(68, 68);
                Img(disc, "sl_disc_peach", Color.white, simple: true);
                var icon = Rect("Icon", disc);
                icon.sizeDelta = new Vector2(48, 48);
                var iconImage = Img(icon, "icon_arrow", Color.white);

                var title = Label("Title", card, "Goal", 25, _body, null, Brown, TextAlignmentOptions.Left);
                title.textWrappingMode = TextWrappingModes.Normal;
                title.lineSpacing = -12f;
                title.fontSizeMin = 21;
                title.fontSizeMax = 25;
                title.enableAutoSizing = true;
                Fill(title.rectTransform, new Vector2(90, 40), new Vector2(-126, -6));

                var barBg = Rect("Bar", card);
                barBg.anchorMin = barBg.anchorMax = barBg.pivot = new Vector2(0f, 0f);
                barBg.anchoredPosition = new Vector2(90, 16);
                barBg.sizeDelta = new Vector2(150, 22);
                Img(barBg, "sl_frame_white", new Color(0.88f, 0.8f, 0.7f), 4f);
                var fillArea = Rect("FillArea", barBg);
                Fill(fillArea, new Vector2(3, 3), new Vector2(-3, -3));
                fillArea.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
                var fill = Rect("Fill", fillArea);
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(0.02f, 1f);
                fill.offsetMin = fill.offsetMax = Vector2.zero;
                Img(fill, "sl_btn_orange", Color.white, 5f);
                var count = Label("Count", card, "0/1", 22, _display, _shadow, SoftBrown, TextAlignmentOptions.Left);
                count.rectTransform.anchorMin = count.rectTransform.anchorMax = count.rectTransform.pivot = new Vector2(0f, 0f);
                count.rectTransform.anchoredPosition = new Vector2(248, 12);
                count.rectTransform.sizeDelta = new Vector2(80, 30);

                var claim = Rect("Claim", card);
                claim.anchorMin = claim.anchorMax = claim.pivot = new Vector2(1f, 0.5f);
                claim.anchoredPosition = new Vector2(-12, 0);
                claim.sizeDelta = new Vector2(112, 66);
                var claimImage = Img(claim, "sl_frame_peach", Color.white, 2.2f);
                claimImage.raycastTarget = true;
                var claimButton = claim.gameObject.AddComponent<UnityEngine.UI.Button>();
                claimButton.targetGraphic = claimImage;
                var colors = claimButton.colors;
                colors.disabledColor = Color.white;
                claimButton.colors = colors;
                var juice = claim.gameObject.AddComponent<ButtonJuice>();
                var rewardIcon = Rect("RewardIcon", claim);
                rewardIcon.anchorMin = rewardIcon.anchorMax = new Vector2(0f, 0.5f);
                rewardIcon.anchoredPosition = new Vector2(28, 3);
                rewardIcon.sizeDelta = new Vector2(38, 38);
                var rewardIconImage = Img(rewardIcon, "icon_gem", Color.white);
                var rewardText = Label("Amount", claim, "+15", 30, _display, _outlined, Color.white, TextAlignmentOptions.Center);
                Fill(rewardText.rectTransform, new Vector2(44, 6), new Vector2(-6, 0));

                cards.Add(new GoalsPanel.Card
                {
                    root = card, icon = iconImage, title = title, count = count, fill = fill,
                    claim = claimButton, claimImage = claimImage, rewardIcon = rewardIconImage,
                    rewardText = rewardText, juice = juice,
                });
            }

            var so = new SerializedObject(panel);
            var prop = so.FindProperty("cards");
            prop.arraySize = cards.Count;
            for (int i = 0; i < cards.Count; i++)
            {
                var e = prop.GetArrayElementAtIndex(i);
                var c = cards[i];
                e.FindPropertyRelative("root").objectReferenceValue = c.root;
                e.FindPropertyRelative("icon").objectReferenceValue = c.icon;
                e.FindPropertyRelative("title").objectReferenceValue = c.title;
                e.FindPropertyRelative("count").objectReferenceValue = c.count;
                e.FindPropertyRelative("fill").objectReferenceValue = c.fill;
                e.FindPropertyRelative("claim").objectReferenceValue = c.claim;
                e.FindPropertyRelative("claimImage").objectReferenceValue = c.claimImage;
                e.FindPropertyRelative("rewardIcon").objectReferenceValue = c.rewardIcon;
                e.FindPropertyRelative("rewardText").objectReferenceValue = c.rewardText;
                e.FindPropertyRelative("juice").objectReferenceValue = c.juice;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            Set(panel, "group", group);
            Set(panel, "waitingMaterial", _shadow);
            Set(panel, "claimReady", Resolve("sl_btn_orange"));
            Set(panel, "claimWaiting", Resolve("sl_frame_peach"));
            Set(panel, "coinIcon", Resolve("icon_coin"));
            Set(panel, "gemIcon", Resolve("icon_gem"));
            Set(panel, "levelIcon", Resolve("icon_arrow"));
            Set(panel, "serveIcon", Resolve("sl_heart"));
            Set(panel, "managerIcon", Resolve("icon_manager"));
            Set(panel, "machineIcon", Resolve("icon_machine"));
            Set(panel, "shopLevelIcon", Resolve("sl_medal"));
            Set(panel, "renovateIcon", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Icons/renovate.png"));
            return panel;
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
            Img(ring, "sl_btn_white_circle", new Color(0.62f, 0.42f, 0.32f), simple: true);
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

            var tap = Pin(root, "TapPin", "TAP!");

            // Upgrade badge: a small green bubble with an up arrow on the ring's corner; opens the card.
            var upBadge = Rect("UpgradeBadge", root);
            upBadge.anchoredPosition = new Vector2(46, 40);
            upBadge.sizeDelta = new Vector2(54, 54);
            var upBadgeImage = Img(upBadge, "sl_green_disc", Color.white);
            upBadgeImage.raycastTarget = true;
            var upBadgeButton = upBadge.gameObject.AddComponent<UnityEngine.UI.Button>();
            upBadgeButton.targetGraphic = upBadgeImage;
            upBadge.gameObject.AddComponent<ButtonJuice>();
            var up = Rect("Arrow", upBadge);
            up.sizeDelta = new Vector2(30, 30);
            up.anchoredPosition = new Vector2(0, 1);
            up.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Img(up, "sl_play", Color.white);

            var badge = Rect("ManagerBadge", root);
            badge.sizeDelta = new Vector2(150, 56);
            badge.anchoredPosition = new Vector2(0, -74);
            Img(badge, "sl_btn_pink", Color.white, 2.2f);
            var hat = Rect("Hat", badge);
            hat.anchorMin = hat.anchorMax = new Vector2(0f, 0.5f);
            hat.anchoredPosition = new Vector2(26, 2);
            hat.sizeDelta = new Vector2(46, 46);
            Img(hat, "icon_manager", Color.white);
            var auto = Label("Label", badge, "AUTO", 30, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            Fill(auto.rectTransform, new Vector2(40, 4), new Vector2(-8, 0));

            var stockPill = Rect("StockPill", root);
            stockPill.sizeDelta = new Vector2(96, 46);
            stockPill.anchoredPosition = new Vector2(-96, -4);
            Window(stockPill, 5f, 2.4f);
            var stock = Label("Stock", stockPill, "", 32, _display, _shadow, Brown, TextAlignmentOptions.Center);
            Stretch(stock.rectTransform);
            stockPill.gameObject.AddComponent<HideWhenEmpty>().label = stock;

            Set(hud, "ring", ring.gameObject);
            Set(hud, "ringFill", fill);
            Set(hud, "tapPin", tap);
            Set(hud, "upgradeBadge", upBadge);
            Set(hud, "managerBadge", badge.gameObject);
            Set(hud, "stockText", stock);
            _markers.Add(hud);
        }

        // A glossy orange "map pin" whose tip (the pivot) touches what should be tapped.
        static RectTransform Pin(RectTransform parent, string name, string label)
        {
            var pin = Rect(name, parent);
            pin.pivot = new Vector2(0.5f, 0f);
            pin.sizeDelta = new Vector2(108, 140);
            var tail = Rect("Tail", pin);
            tail.anchorMin = tail.anchorMax = new Vector2(0.5f, 0f);
            tail.anchoredPosition = new Vector2(0, 22);
            tail.sizeDelta = new Vector2(50, 50);
            tail.localRotation = Quaternion.Euler(0f, 0f, -90f);
            Img(tail, "sl_play", PinOrange);
            var disc = Rect("Disc", pin);
            disc.anchorMin = disc.anchorMax = new Vector2(0.5f, 0f);
            disc.pivot = new Vector2(0.5f, 0f);
            disc.anchoredPosition = new Vector2(0, 30);
            disc.sizeDelta = new Vector2(108, 108);
            Img(disc, "sl_btn_orange_circle", Color.white, simple: true);
            var text = Label("Label", disc, label, 36, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            Fill(text.rectTransform, new Vector2(0, 8), new Vector2(0, 0));
            return pin;
        }

        static string IconFor(StationView s) => s.name switch
        {
            "FlourStorage" => "icon_output",
            "Mixer" => "icon_machine",
            "Oven" => "sl_icon_speed",
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
            banner.anchoredPosition = new Vector2(-32, -24);
            banner.sizeDelta = new Vector2(560, 112);
            Img(banner, "sl_banner", Color.white, BannerSlice(112f));
            var name = Label("Name", banner, ShopName, 50, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            BandText(name.rectTransform, 112f);
            return bar;
        }

        static RectTransform Counter(RectTransform bar, string name, Vector2 pos, float width, string icon, out TMP_Text label, out RectTransform iconRect)
        {
            var pill = Rect(name, bar);
            pill.anchorMin = pill.anchorMax = new Vector2(0f, 1f);
            pill.pivot = new Vector2(0f, 0.5f);
            pill.anchoredPosition = pos + new Vector2(0, -50);
            pill.sizeDelta = new Vector2(width, 80);
            Window(pill, 7f, 1.3f);
            iconRect = Rect("Icon", pill);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-6, 2);
            iconRect.sizeDelta = new Vector2(108, 108);
            Img(iconRect, icon, Color.white);
            label = Label("Value", pill, "0", 50, _display, _shadow, Brown, TextAlignmentOptions.Left);
            Fill(label.rectTransform, new Vector2(104, 4), new Vector2(-24, 0));
            return pill;
        }

        // ------------------------------------------------------------------ Station card

        static StationPanel StationCard(RectTransform root)
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
            Window(bg).raycastTarget = true;

            var ribbon = Rect("Ribbon", sheet);
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0, 14);
            ribbon.sizeDelta = new Vector2(500, 108);
            Img(ribbon, "sl_banner", Color.white, BannerSlice(108f));
            var title = Label("Title", ribbon, "Station", 50, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            BandText(title.rectTransform, 108f);

            var info = Label("Info", sheet, "", 28, _body, null, SoftBrown, TextAlignmentOptions.Center);
            Top(info.rectTransform, new Vector2(0, -64), new Vector2(720, 40), centered: true);

            var closeBtn = CloseButton(sheet);

            var rows = new List<StationPanel.UpgradeRow>();
            (UpgradeTrack track, string icon)[] defs = { (UpgradeTrack.Speed, "sl_icon_speed"), (UpgradeTrack.Output, "icon_output"), (UpgradeTrack.Machines, "icon_machine") };
            for (int i = 0; i < defs.Length; i++)
                rows.Add(Row(sheet, defs[i].track, defs[i].icon, -134 - i * 108, "icon_coin", "sl_btn_orange"));
            var mgr = Row(sheet, UpgradeTrack.Speed, "icon_manager", -134 - 3 * 108, "icon_gem", "sl_btn_pink", manager: true);

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
            Set(panel, "affordableSprite", Resolve("sl_btn_orange"));
            Set(panel, "unaffordableSprite", Resolve("sl_btn_white"));
            Set(panel, "maxedSprite", Resolve("sl_btn_yellow"));
            Set(panel, "managerSprite", Resolve("sl_btn_pink"));
            return panel;
        }

        static StationPanel.UpgradeRow Row(RectTransform sheet, UpgradeTrack track, string icon, float y, string costIcon, string buttonSprite, bool manager = false)
        {
            var row = Rect(manager ? "ManagerRow" : $"Row_{track}", sheet);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0, y);
            row.sizeDelta = new Vector2(-56, 98);
            Img(row, "sl_frame_white", manager ? new Color(1f, 0.9f, 0.92f) : Color.white, 1.5f);

            var iconBg = Rect("IconBg", row);
            iconBg.anchorMin = iconBg.anchorMax = new Vector2(0f, 0.5f);
            iconBg.anchoredPosition = new Vector2(54, 0);
            iconBg.sizeDelta = new Vector2(78, 78);
            Img(iconBg, manager ? "sl_disc_pink" : "sl_disc_peach", Color.white, simple: true);
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
                Img(chip, "sl_frame_peach", Color.white, 2.6f);
                level = Label("Text", chip, "Lv 0", 24, _display, null, Brown, TextAlignmentOptions.Center);
                Stretch(level.rectTransform);
            }

            var btn = Rect("Buy", row);
            btn.anchorMin = btn.anchorMax = new Vector2(1f, 0.5f);
            btn.pivot = new Vector2(1f, 0.5f);
            btn.anchoredPosition = new Vector2(-12, 0);
            btn.sizeDelta = new Vector2(210, 82);
            var image = Img(btn, buttonSprite, Color.white, 1.5f);
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
            Window(Stretch(Rect("Background", card)), 16f);
            var ribbon = Rect("Ribbon", card);
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0, 16);
            ribbon.sizeDelta = new Vector2(580, 120);
            Img(ribbon, "sl_banner", Color.white, BannerSlice(120f));
            var title = Label("Title", ribbon, "Welcome back!", 56, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            BandText(title.rectTransform, 120f);

            var clock = Rect("Clock", card);
            clock.anchorMin = clock.anchorMax = new Vector2(0.5f, 1f);
            clock.anchoredPosition = new Vector2(0, -150);
            clock.sizeDelta = new Vector2(120, 120);
            Img(clock, "icon_clock", Color.white);
            var body = Label("Body", card, "", 32, _body, null, SoftBrown, TextAlignmentOptions.Center);
            body.textWrappingMode = TextWrappingModes.Normal;
            Top(body.rectTransform, new Vector2(0, -222), new Vector2(660, 96), centered: true);

            var amountRow = Rect("AmountRow", card);
            amountRow.anchorMin = amountRow.anchorMax = new Vector2(0.5f, 0f);
            amountRow.anchoredPosition = new Vector2(0, 200);
            amountRow.sizeDelta = new Vector2(500, 110);
            var shine = Rect("Shine", amountRow);
            shine.anchorMin = shine.anchorMax = new Vector2(0f, 0.5f);
            shine.anchoredPosition = new Vector2(60, 0);
            shine.sizeDelta = new Vector2(260, 260);
            Img(shine, "sl_shine", new Color(1f, 0.72f, 0.3f, 0.7f));
            shine.gameObject.AddComponent<UiSpin>();
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
            button.targetGraphic = Img(btn, "sl_btn_orange", Color.white, 1.1f);
            btn.gameObject.AddComponent<ButtonJuice>();
            var label = Label("Label", btn, "Collect", 56, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            Fill(label.rectTransform, new Vector2(10, 8), new Vector2(-10, 0));

            Set(dialog, "root", dim.gameObject);
            Set(dialog, "body", body);
            Set(dialog, "amount", amount);
            Set(dialog, "collectButton", button);
        }

        // ------------------------------------------------------------------ Merge board

        /// <summary>The renovation board panel (right side) and its Renovate button (bottom right).</summary>
        public static void BuildBoard(MergeBoardDefinition def, Sprite[][] chainIcons, Sprite[] generatorIcons, Sprite openIcon, CoinFlyLayer coinFly)
        {
            var holder = Stretch(Rect("Board", _root));
            var tutorial = _root.Find("Tutorial");
            if (tutorial != null)
                holder.SetSiblingIndex(tutorial.GetSiblingIndex());
            var view = holder.gameObject.AddComponent<MergeBoardView>();

            // Renovate button.
            var open = Rect("RenovateButton", holder);
            open.anchorMin = open.anchorMax = open.pivot = new Vector2(1f, 0f);
            open.anchoredPosition = new Vector2(-36, 44);
            open.sizeDelta = new Vector2(150, 150);
            var openImage = Img(open, "sl_btn_orange_circle", Color.white, simple: true);
            openImage.raycastTarget = true;
            var openButton = open.gameObject.AddComponent<UnityEngine.UI.Button>();
            openButton.targetGraphic = openImage;
            open.gameObject.AddComponent<ButtonJuice>();
            var openArt = Rect("Icon", open);
            openArt.anchoredPosition = new Vector2(0, 14);
            openArt.sizeDelta = new Vector2(104, 104);
            var openArtImage = openArt.gameObject.AddComponent<UnityEngine.UI.Image>();
            openArtImage.sprite = openIcon;
            openArtImage.preserveAspect = true;
            openArtImage.raycastTarget = false;
            var openLabel = Label("Label", open, "Renovate", 30, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            openLabel.rectTransform.anchorMin = openLabel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            openLabel.rectTransform.anchoredPosition = new Vector2(0, 8);
            openLabel.rectTransform.sizeDelta = new Vector2(200, 44);
            var alert = Rect("Alert", open);
            alert.anchorMin = alert.anchorMax = new Vector2(1f, 1f);
            alert.anchoredPosition = new Vector2(-18, -18);
            alert.sizeDelta = new Vector2(48, 48);
            Img(alert, "sl_btn_red_circle", Color.white, simple: true);
            var bang = Label("Text", alert, "!", 34, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            Fill(bang.rectTransform, new Vector2(0, 4), Vector2.zero);

            // Soft dark backdrop; clicking it closes the board.
            var dim = Stretch(Rect("Dim", holder));
            var dimImage = Img(dim, null, new Color(0.03f, 0.02f, 0.02f, 0.55f));
            dimImage.raycastTarget = true;
            var dimButton = dim.gameObject.AddComponent<UnityEngine.UI.Button>();
            dimButton.transition = UnityEngine.UI.Selectable.Transition.None;
            var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();

            // Panel.
            var panel = Rect("Panel", holder);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 0.5f);
            panel.anchoredPosition = new Vector2(-30, -22);
            panel.sizeDelta = new Vector2(860, 950);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            Window(Stretch(Rect("Background", panel)), 16f).raycastTarget = true;

            var ribbon = Rect("Ribbon", panel);
            ribbon.anchorMin = ribbon.anchorMax = new Vector2(0.5f, 1f);
            ribbon.anchoredPosition = new Vector2(0, 12);
            ribbon.sizeDelta = new Vector2(460, 104);
            Img(ribbon, "sl_banner", Color.white, BannerSlice(104f));
            var title = Label("Title", ribbon, "Renovation", 50, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            BandText(title.rectTransform, 104f);

            var closeButton = CloseButton(panel);

            // Energy and coziness pills.
            RectTransform Pill(string name, float xPos, float width, string icon, out TMP_Text text)
            {
                var pill = Rect(name, panel);
                pill.anchorMin = pill.anchorMax = pill.pivot = new Vector2(0f, 1f);
                pill.anchoredPosition = new Vector2(xPos, -72);
                pill.sizeDelta = new Vector2(width, 60);
                Window(pill, 5f, 1.6f);
                var ic = Rect("Icon", pill);
                ic.anchorMin = ic.anchorMax = new Vector2(0f, 0.5f);
                ic.anchoredPosition = new Vector2(4, 2);
                ic.sizeDelta = new Vector2(70, 70);
                Img(ic, icon, Color.white);
                text = Label("Value", pill, "", 32, _display, _shadow, Brown, TextAlignmentOptions.Left);
                Fill(text.rectTransform, new Vector2(48, 2), new Vector2(-10, 0));
                return pill;
            }
            var energyPill = Pill("Energy", 40, 330, "icon_speed", out var energyText);
            var timer = Label("Timer", energyPill, "Full", 24, _body, null, SoftBrown, TextAlignmentOptions.Right);
            Fill(timer.rectTransform, new Vector2(150, 2), new Vector2(-18, 0));
            Pill("Coziness", 860 - 40 - 290 - 54, 290, "sl_heart", out var cozyText);

            // Renovation tasks.
            var tasks = new List<MergeBoardView.Task>();
            for (int i = 0; i < 3; i++)
            {
                var card = Rect($"Task{i}", panel);
                card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
                card.pivot = new Vector2(0.5f, 1f);
                card.anchoredPosition = new Vector2(34 + 128 + i * 268, -146);
                card.sizeDelta = new Vector2(256, 196);
                Img(card, "sl_frame_white", Color.white, 1.4f);
                var tt = Label("Title", card, "Renovation", 24, _body, null, Brown, TextAlignmentOptions.Center);
                tt.textWrappingMode = TextWrappingModes.Normal;
                tt.enableAutoSizing = true;
                tt.fontSizeMin = 19;
                tt.fontSizeMax = 24;
                tt.lineSpacing = -14f;
                Top(tt.rectTransform, new Vector2(0, -6), new Vector2(232, 50), centered: true);
                var bonus = Label("Bonus", card, "+5% cozy", 22, _display, _shadow, new Color(0.2f, 0.52f, 0.16f), TextAlignmentOptions.Center);
                Top(bonus.rectTransform, new Vector2(0, -52), new Vector2(232, 26), centered: true);

                var frames = new List<UnityEngine.UI.Image>();
                var needIcons = new List<UnityEngine.UI.Image>();
                var checks = new List<GameObject>();
                for (int n = 0; n < 3; n++)
                {
                    var slot = Rect($"Need{n}", card);
                    slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 1f);
                    slot.anchoredPosition = new Vector2((n - 1) * 70, -110);
                    slot.sizeDelta = new Vector2(62, 62);
                    frames.Add(Img(slot, "sl_frame_cream", Color.white, 3f));
                    var ni = Rect("Icon", slot);
                    ni.sizeDelta = new Vector2(56, 56);
                    var niImg = ni.gameObject.AddComponent<UnityEngine.UI.Image>();
                    niImg.preserveAspect = true;
                    niImg.raycastTarget = false;
                    needIcons.Add(niImg);
                    var check = Rect("Check", slot);
                    check.anchorMin = check.anchorMax = new Vector2(1f, 0f);
                    check.anchoredPosition = new Vector2(-4, 6);
                    check.sizeDelta = new Vector2(28, 28);
                    Img(check, "sl_green_disc", Color.white);
                    checks.Add(check.gameObject);
                }

                var fix = Rect("Fix", card);
                fix.anchorMin = fix.anchorMax = new Vector2(0.5f, 0f);
                fix.pivot = new Vector2(0.5f, 0f);
                fix.anchoredPosition = new Vector2(0, 8);
                fix.sizeDelta = new Vector2(196, 46);
                var fixImage = Img(fix, "sl_btn_orange", Color.white, 2.6f);
                fixImage.raycastTarget = true;
                var fixButton = fix.gameObject.AddComponent<UnityEngine.UI.Button>();
                fixButton.targetGraphic = fixImage;
                var colors = fixButton.colors;
                colors.disabledColor = Color.white;
                fixButton.colors = colors;
                fix.gameObject.AddComponent<ButtonJuice>();
                var fixText = Label("Text", fix, "Fix", 26, _display, _shadow, Color.white, TextAlignmentOptions.Center);
                Fill(fixText.rectTransform, new Vector2(4, 4), Vector2.zero);

                tasks.Add(new MergeBoardView.Task
                {
                    root = card, title = tt, bonus = bonus, needFrames = frames.ToArray(), needIcons = needIcons.ToArray(),
                    needChecks = checks.ToArray(), fix = fixButton, fixImage = fixImage, fixText = fixText,
                });
            }

            // The grid.
            const float cell = 92f, gap = 6f;
            var grid = Rect("Grid", panel);
            grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            float gridW = def.columns * cell + (def.columns - 1) * gap + 24f;
            float gridH = def.rows * cell + (def.rows - 1) * gap + 24f;
            grid.anchoredPosition = new Vector2(0, -356);
            grid.sizeDelta = new Vector2(gridW, gridH);
            Img(grid, "sl_frame_white", new Color(0.86f, 0.66f, 0.46f), 1.2f);
            var cellRects = new List<RectTransform>();
            var frameImages = new List<UnityEngine.UI.Image>();
            var iconImages = new List<UnityEngine.UI.Image>();
            for (int r = 0; r < def.rows; r++)
            {
                for (int c = 0; c < def.columns; c++)
                {
                    var rt = Rect($"Cell{r * def.columns + c}", grid);
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(12 + c * (cell + gap), -12 - r * (cell + gap));
                    rt.sizeDelta = new Vector2(cell, cell);
                    var frame = Img(rt, "sl_frame_cream", Color.white, 2.2f);
                    frame.raycastTarget = true;
                    var ic = Rect("Icon", rt);
                    ic.sizeDelta = new Vector2(cell - 8, cell - 8);
                    var icImg = ic.gameObject.AddComponent<UnityEngine.UI.Image>();
                    icImg.preserveAspect = true;
                    icImg.raycastTarget = false;
                    cellRects.Add(rt);
                    frameImages.Add(frame);
                    iconImages.Add(icImg);
                }
            }

            // Info bar under the grid.
            var info = Label("Info", panel, "", 25, _body, null, Brown, TextAlignmentOptions.Left);
            info.textWrappingMode = TextWrappingModes.Normal;
            info.enableAutoSizing = true;
            info.fontSizeMin = 20;
            info.fontSizeMax = 25;
            info.rectTransform.anchorMin = info.rectTransform.anchorMax = info.rectTransform.pivot = new Vector2(0f, 0f);
            info.rectTransform.anchoredPosition = new Vector2(40, 22);
            info.rectTransform.sizeDelta = new Vector2(600, 64);
            var sell = Rect("Sell", panel);
            sell.anchorMin = sell.anchorMax = sell.pivot = new Vector2(1f, 0f);
            sell.anchoredPosition = new Vector2(-36, 26);
            sell.sizeDelta = new Vector2(170, 56);
            var sellImage = Img(sell, "sl_btn_yellow", Color.white, 2.4f);
            sellImage.raycastTarget = true;
            var sellButton = sell.gameObject.AddComponent<UnityEngine.UI.Button>();
            sellButton.targetGraphic = sellImage;
            sell.gameObject.AddComponent<ButtonJuice>();
            var sellText = Label("Text", sell, "Sell +5", 28, _display, _shadow, Brown, TextAlignmentOptions.Center);
            Fill(sellText.rectTransform, new Vector2(4, 4), Vector2.zero);

            // Effects on top: drag ghost, spawn flyer, merge burst.
            Img(Rect("Burst", panel), "sl_shine", Color.white).rectTransform.sizeDelta = new Vector2(200, 200);
            var burst = (RectTransform)panel.Find("Burst");
            var ghost = Rect("DragGhost", panel);
            ghost.sizeDelta = new Vector2(cell + 10, cell + 10);
            var ghostImg = ghost.gameObject.AddComponent<UnityEngine.UI.Image>();
            ghostImg.preserveAspect = true;
            ghostImg.raycastTarget = false;
            var fly = Rect("Flyer", panel);
            fly.sizeDelta = new Vector2(cell - 8, cell - 8);
            var flyImg = fly.gameObject.AddComponent<UnityEngine.UI.Image>();
            flyImg.preserveAspect = true;
            flyImg.raycastTarget = false;

            // Wire up.
            Set(view, "panel", panel);
            Set(view, "group", group);
            Set(view, "openButton", openButton);
            Set(view, "openAlert", alert.gameObject);
            Set(view, "closeButton", closeButton);
            Set(view, "dim", dimGroup);
            Set(view, "dimButton", dimButton);
            var banner = _root.Find("ShopBanner");
            if (banner != null)
                SetArray(view, "hideWhileOpen", new Object[] { banner.gameObject.AddComponent<CanvasGroup>() });
            SetArray(view, "cells", cellRects.ToArray());
            SetArray(view, "cellFrames", frameImages.ToArray());
            SetArray(view, "icons", iconImages.ToArray());
            Set(view, "dragGhost", ghostImg);
            Set(view, "flyer", flyImg);
            Set(view, "burst", burst);
            Set(view, "energyText", energyText);
            Set(view, "energyTimer", timer);
            Set(view, "cozinessText", cozyText);
            Set(view, "infoText", info);
            Set(view, "sellButton", sellButton);
            Set(view, "sellText", sellText);
            Set(view, "frameNormal", Resolve("sl_frame_cream"));
            Set(view, "frameSelected", Resolve("sl_frame_peach"));
            Set(view, "frameGenerator", Resolve("sl_frame_honey"));
            Set(view, "fixReady", Resolve("sl_btn_orange"));
            Set(view, "fixWaiting", Resolve("sl_btn_white"));
            Set(view, "coinFly", coinFly);
            SetArray(view, "generatorIcons", generatorIcons);

            var so = new SerializedObject(view);
            var chainsProp = so.FindProperty("chainIcons");
            chainsProp.arraySize = chainIcons.Length;
            for (int c = 0; c < chainIcons.Length; c++)
            {
                var tiersProp = chainsProp.GetArrayElementAtIndex(c).FindPropertyRelative("tiers");
                tiersProp.arraySize = chainIcons[c].Length;
                for (int t = 0; t < chainIcons[c].Length; t++)
                    tiersProp.GetArrayElementAtIndex(t).objectReferenceValue = chainIcons[c][t];
            }
            var tasksProp = so.FindProperty("tasks");
            tasksProp.arraySize = tasks.Count;
            for (int i = 0; i < tasks.Count; i++)
            {
                var e = tasksProp.GetArrayElementAtIndex(i);
                var t = tasks[i];
                e.FindPropertyRelative("root").objectReferenceValue = t.root;
                e.FindPropertyRelative("title").objectReferenceValue = t.title;
                e.FindPropertyRelative("bonus").objectReferenceValue = t.bonus;
                void Arr(string name, Object[] values)
                {
                    var a = e.FindPropertyRelative(name);
                    a.arraySize = values.Length;
                    for (int k = 0; k < values.Length; k++)
                        a.GetArrayElementAtIndex(k).objectReferenceValue = values[k];
                }
                Arr("needFrames", t.needFrames);
                Arr("needIcons", t.needIcons);
                Arr("needChecks", t.needChecks);
                e.FindPropertyRelative("fix").objectReferenceValue = t.fix;
                e.FindPropertyRelative("fixImage").objectReferenceValue = t.fixImage;
                e.FindPropertyRelative("fixText").objectReferenceValue = t.fixText;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ Tutorial

        const string SpotlightMaterialPath = "Assets/Art/UI/Spotlight.mat";

        static Material SpotlightMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SpotlightMaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("MergeResto/UI/Spotlight"));
                AssetDatabase.CreateAsset(mat, SpotlightMaterialPath);
            }
            mat.color = new Color(0.2f, 0.11f, 0.08f, 0.5f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>The Grand Opening layer: spotlight, pointer, Grandma's speech box, progress bar.</summary>
        public static void BuildTutorial(StationView[] stations, FxDirector fx, CoinFlyLayer coinFly, ChibiAnimator grandma, Sprite portraitSprite,
            CustomerCrowd crowd, CameraDirector cameraDirector)
        {
            var holder = Stretch(Rect("Tutorial", _root));
            var dialog = _root.Find("OfflineDialog");
            if (dialog != null)
                holder.SetSiblingIndex(dialog.GetSiblingIndex());
            var group = holder.gameObject.AddComponent<CanvasGroup>();
            var tutorial = holder.gameObject.AddComponent<GrandOpeningTutorial>();

            // Dim with a soft hole over the target.
            var dim = Rect("Spotlight", holder);
            dim.sizeDelta = new Vector2(6000, 6000);
            var dimImage = Img(dim, null, Color.white);
            dimImage.material = SpotlightMaterial();
            var dimButton = dim.gameObject.AddComponent<UnityEngine.UI.Button>();
            dimButton.transition = UnityEngine.UI.Selectable.Transition.None;
            dimButton.targetGraphic = dimImage;

            var ping = Rect("Ping", holder);
            ping.sizeDelta = new Vector2(150, 150);
            Img(ping, "sl_ping", Color.white, simple: true);

            // Grandma's speech box, bottom left; it slides right while the upgrade card is open.
            var speech = Rect("Speech", holder);
            speech.anchorMin = speech.anchorMax = speech.pivot = new Vector2(0f, 0f);
            speech.anchoredPosition = new Vector2(36, 32);
            speech.sizeDelta = new Vector2(900, 236);
            var frame = Window(speech, 12f);
            frame.raycastTarget = true;
            var speechButton = speech.gameObject.AddComponent<UnityEngine.UI.Button>();
            speechButton.transition = UnityEngine.UI.Selectable.Transition.None;
            speechButton.targetGraphic = frame;

            var portrait = Rect("Portrait", speech);
            portrait.anchorMin = portrait.anchorMax = new Vector2(0f, 0.5f);
            portrait.anchoredPosition = new Vector2(132, 14);
            portrait.sizeDelta = new Vector2(226, 226);
            Img(portrait, "sl_disc_honey", Color.white, simple: true);
            var mask = Rect("Mask", portrait);
            mask.sizeDelta = new Vector2(198, 198);
            Img(mask, "sl_disc_white", Color.white, simple: true);
            mask.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
            var face = Rect("Face", mask);
            face.sizeDelta = new Vector2(198, 198);
            var faceImage = face.gameObject.AddComponent<UnityEngine.UI.Image>();
            faceImage.sprite = portraitSprite;
            faceImage.raycastTarget = false;

            var plate = Rect("NamePlate", speech);
            plate.anchorMin = plate.anchorMax = new Vector2(0f, 1f);
            plate.pivot = new Vector2(0f, 0.5f);
            plate.anchoredPosition = new Vector2(236, 4);
            plate.sizeDelta = new Vector2(250, 68);
            Img(plate, "sl_banner", Color.white, BannerSlice(68f));
            var plateText = Label("Name", plate, "Grandma", 36, _display, _outlined, Color.white, TextAlignmentOptions.Center);
            Fill(plateText.rectTransform, new Vector2(30, 6), new Vector2(-30, -4));

            var text = Label("Text", speech, "", 34, _body, null, Brown, TextAlignmentOptions.Left);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.lineSpacing = -6f;
            Fill(text.rectTransform, new Vector2(262, 54), new Vector2(-40, -46));

            var hint = Rect("ContinueHint", speech);
            hint.anchorMin = hint.anchorMax = hint.pivot = new Vector2(1f, 0f);
            hint.anchoredPosition = new Vector2(-34, 16);
            hint.sizeDelta = new Vector2(320, 40);
            var hintText = Label("Text", hint, "Tap to continue", 28, _display, _shadow, PinOrange, TextAlignmentOptions.Right);
            Fill(hintText.rectTransform, Vector2.zero, new Vector2(-40, 0));
            var hintArrow = Rect("Arrow", hint);
            hintArrow.anchorMin = hintArrow.anchorMax = new Vector2(1f, 0.5f);
            hintArrow.anchoredPosition = new Vector2(-14, 0);
            hintArrow.sizeDelta = new Vector2(30, 30);
            Img(hintArrow, "sl_play", PinOrange);

            // Progress bar, top centre.
            var bar = Rect("Progress", holder);
            // Top left under the counters, where the goals sit after the tutorial; the top
            // centre stays clear for pins over the back stations.
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0f, 1f);
            bar.anchoredPosition = new Vector2(34, -140);
            bar.sizeDelta = new Vector2(470, 76);
            Window(bar, 7f, 1.3f);
            var fillArea = Rect("FillArea", bar);
            Fill(fillArea, new Vector2(11, 11), new Vector2(-11, -11));
            fillArea.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var fill = Rect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.001f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            Img(fill, "sl_btn_orange", Color.white, 2.4f);
            var barText = Label("Text", bar, "Grand Opening  0/6", 34, _display, _shadow, Brown, TextAlignmentOptions.Center);
            Fill(barText.rectTransform, new Vector2(10, 4), new Vector2(-60, 0));
            var medal = Rect("Medal", bar);
            medal.anchorMin = medal.anchorMax = new Vector2(1f, 0.5f);
            medal.anchoredPosition = new Vector2(4, 4);
            medal.sizeDelta = new Vector2(96, 92);
            Img(medal, "sl_medal", Color.white);
            var barJuice = bar.gameObject.AddComponent<ButtonJuice>();

            // The pointer goes on top of everything else in the layer.
            var pointer = Pin(holder, "Pointer", "TAP!");

            var stationArray = new UnityEngine.Object[stations.Length];
            var markerArray = new UnityEngine.Object[_markers.Count];
            for (int i = 0; i < stations.Length; i++) stationArray[i] = stations[i];
            for (int i = 0; i < _markers.Count; i++) markerArray[i] = _markers[i];
            SetArray(tutorial, "stations", stationArray);
            SetArray(tutorial, "markers", markerArray);
            Set(tutorial, "panel", _panel);
            Set(tutorial, "coinFly", coinFly);
            Set(tutorial, "fx", fx);
            Set(tutorial, "grandma", grandma);
            Set(tutorial, "crowd", crowd);
            Set(tutorial, "cameraDirector", cameraDirector);
            Set(tutorial, "group", group);
            Set(tutorial, "layer", holder);
            Set(tutorial, "dim", dimImage);
            Set(tutorial, "dimButton", dimButton);
            Set(tutorial, "pointer", pointer);
            Set(tutorial, "ping", ping);
            Set(tutorial, "speech", speech);
            Set(tutorial, "speechButton", speechButton);
            Set(tutorial, "speechText", text);
            Set(tutorial, "continueHint", hint.gameObject);
            Set(tutorial, "portrait", portrait);
            Set(tutorial, "progressRoot", bar);
            Set(tutorial, "progressFill", fill);
            Set(tutorial, "progressText", barText);
            Set(tutorial, "progressJuice", barJuice);
        }

        static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ Helpers

        /// <summary>
        /// Images are built with raycasts off (so decoration never steals clicks); every button's
        /// own graphic must receive them, or the button can never be pressed.
        /// </summary>
        public static void EnableButtonTargets()
        {
            foreach (var button in _root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (button.targetGraphic == null)
                    button.targetGraphic = button.GetComponent<UnityEngine.UI.Graphic>();
                if (button.targetGraphic != null)
                    button.targetGraphic.raycastTarget = true;
            }
        }

        // Every window's close button: a red round button sitting inside the top-right corner.
        static UnityEngine.UI.Button CloseButton(RectTransform window)
        {
            var close = Rect("Close", window);
            close.anchorMin = close.anchorMax = close.pivot = new Vector2(1f, 1f);
            close.anchoredPosition = new Vector2(-22, -22);
            close.sizeDelta = new Vector2(64, 64);
            var button = close.gameObject.AddComponent<UnityEngine.UI.Button>();
            var bg = Img(close, "sl_btn_red_circle", Color.white, simple: true);
            bg.raycastTarget = true;
            button.targetGraphic = bg;
            var x = Rect("X", close);
            x.sizeDelta = new Vector2(38, 38);
            x.anchoredPosition = new Vector2(0, 1);
            Img(x, "sl_icon_close", Color.white);
            close.gameObject.AddComponent<ButtonJuice>();
            return button;
        }

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
            rt.offsetMin = new Vector2(70f, height * 0.12f);
            rt.offsetMax = new Vector2(-70f, -height * 0.12f);
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

        static UnityEngine.UI.Image Img(RectTransform rt, string sprite, Color color, float slice = 1.6f, bool simple = false)
        {
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            var s = sprite != null ? Resolve(sprite) : null;
            if (sprite != null && s == null)
                Debug.LogError($"[Merge Resto] Missing UI sprite '{sprite}'.");
            img.sprite = s;
            img.color = color;
            bool sliced = !simple && s != null && s.border.sqrMagnitude > 0f;
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.pixelsPerUnitMultiplier = sliced ? slice : 1f;
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
                t.fontStyle = FontStyles.Bold; // synthetic bold: Fredoka ships one weight
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
