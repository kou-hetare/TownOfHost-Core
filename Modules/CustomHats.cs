using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using Il2CppSystem;
using HarmonyLib;
using UnityEngine;
using UnhollowerBaseLib;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;


namespace TownOfHost
{

    [HarmonyPatch]
    public class CustomHats
    {
        public static Material hatShader;

        public static Dictionary<string, HatExtension> CustomHatRegistry = new Dictionary<string, HatExtension>();
        public static HatExtension TestExt = null;

        public class HatExtension
        {
            public string author { get; set; }
            public string package { get; set; }
            public string condition { get; set; }
            public Sprite FlipImage { get; set; }
            public Sprite BackFlipImage { get; set; }
        }

        public class CustomHat
        {
            public string author { get; set; }
            public string package { get; set; }
            public string condition { get; set; }
            public string name { get; set; }
            public string resource { get; set; }
            public string flipresource { get; set; }
            public string backflipresource { get; set; }
            public string backresource { get; set; }
            public string climbresource { get; set; }
            public bool bounce { get; set; }
            public bool adaptive { get; set; }
            public bool behind { get; set; }
        }

        private static List<CustomHat> createCustomHatDetails(string[] hats, bool fromDisk = false)
        {
            Dictionary<string, CustomHat> fronts = new Dictionary<string, CustomHat>();
            Dictionary<string, string> backs = new Dictionary<string, string>();
            Dictionary<string, string> flips = new Dictionary<string, string>();
            Dictionary<string, string> backflips = new Dictionary<string, string>();
            Dictionary<string, string> climbs = new Dictionary<string, string>();

            for (int i = 0; i < hats.Length; i++)
            {
                string s = fromDisk ? hats[i].Substring(hats[i].LastIndexOf("\\") + 1).Split('.')[0] : hats[i].Split('.')[3];
                string[] p = s.Split('_');

                HashSet<string> options = new HashSet<string>();
                for (int j = 1; j < p.Length; j++)
                    options.Add(p[j]);

                if (options.Contains("back") && options.Contains("flip"))
                    backflips.Add(p[0], hats[i]);
                else if (options.Contains("climb"))
                    climbs.Add(p[0], hats[i]);
                else if (options.Contains("back"))
                    backs.Add(p[0], hats[i]);
                else if (options.Contains("flip"))
                    flips.Add(p[0], hats[i]);
                else
                {
                    CustomHat custom = new CustomHat { resource = hats[i] };
                    custom.name = p[0].Replace('-', ' ');
                    custom.bounce = options.Contains("bounce");
                    custom.adaptive = options.Contains("adaptive");
                    custom.behind = options.Contains("behind");

                    fronts.Add(p[0], custom);
                }
            }

            List<CustomHat> customhats = new List<CustomHat>();

            foreach (string k in fronts.Keys)
            {
                CustomHat hat = fronts[k];
                string br, cr, fr, bfr;
                backs.TryGetValue(k, out br);
                climbs.TryGetValue(k, out cr);
                flips.TryGetValue(k, out fr);
                backflips.TryGetValue(k, out bfr);
                if (br != null)
                    hat.backresource = br;
                if (cr != null)
                    hat.climbresource = cr;
                if (fr != null)
                    hat.flipresource = fr;
                if (bfr != null)
                    hat.backflipresource = bfr;
                if (hat.backresource != null)
                    hat.behind = true;

                customhats.Add(hat);
            }

            return customhats;
        }

        private static Sprite CreateHatSprite(string path, bool fromDisk = false)
        {
            Texture2D texture = fromDisk ? Helpers.loadTextureFromDisk(path) : Helpers.loadTextureFromResources(path);
            if (texture == null)
                return null;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.53f, 0.575f), texture.width * 0.375f);
            if (sprite == null)
                return null;
            texture.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        private static HatBehaviour CreateHatBehaviour(CustomHat ch, bool fromDisk = false, bool testOnly = false)
        {
            if (hatShader == null && DestroyableSingleton<HatManager>.InstanceExists)
            {
                foreach (HatBehaviour h in DestroyableSingleton<HatManager>.Instance.AllHats)
                {
                    if (h.AltShader != null)
                    {
                        hatShader = h.AltShader;
                        break;
                    }
                }
            }

            HatBehaviour hat = new HatBehaviour();
            hat.MainImage = CreateHatSprite(ch.resource, fromDisk);
            if (ch.backresource != null)
            {
                hat.BackImage = CreateHatSprite(ch.backresource, fromDisk);
                ch.behind = true; // Required to view backresource
            }
            if (ch.climbresource != null)
                hat.ClimbImage = CreateHatSprite(ch.climbresource, fromDisk);
            hat.name = ch.name + "\nby " + ch.author;
            hat.Order = 99;
            hat.ProductId = "hat_" + ch.name.Replace(' ', '_');
            hat.InFront = !ch.behind;
            hat.NoBounce = !ch.bounce;
            hat.ChipOffset = new Vector2(0f, 0.2f);
            hat.Free = true;
            hat.NotInStore = true;


            if (ch.adaptive && hatShader != null)
                hat.AltShader = hatShader;

            HatExtension extend = new HatExtension();
            extend.author = ch.author != null ? ch.author : "Unknown";
            extend.package = ch.package != null ? ch.package : "Misc.";
            extend.condition = ch.condition != null ? ch.condition : "none";

            if (ch.flipresource != null)
                extend.FlipImage = CreateHatSprite(ch.flipresource, fromDisk);
            if (ch.backflipresource != null)
                extend.BackFlipImage = CreateHatSprite(ch.backflipresource, fromDisk);

            if (testOnly)
            {
                TestExt = extend;
                TestExt.condition = hat.name;
            }
            else
            {
                CustomHatRegistry.Add(hat.name, extend);
            }

            return hat;
        }

        private static HatBehaviour CreateHatBehaviour(CustomHatLoader.CustomHatOnline chd)
        {
            string filePath = Path.GetDirectoryName(Application.dataPath) + @"\TheOtherHats\";
            chd.resource = filePath + chd.resource;
            if (chd.backresource != null)
                chd.backresource = filePath + chd.backresource;
            if (chd.climbresource != null)
                chd.climbresource = filePath + chd.climbresource;
            if (chd.flipresource != null)
                chd.flipresource = filePath + chd.flipresource;
            if (chd.backflipresource != null)
                chd.backflipresource = filePath + chd.backflipresource;
            return CreateHatBehaviour(chd, true);
        }

        [HarmonyPatch(typeof(HatManager), nameof(HatManager.GetHatById))]
        private static class HatManagerPatch
        {
            private static bool LOADED;
            private static bool RUNNING;

            static void Prefix(HatManager __instance)
            {
                if (RUNNING) return;
                RUNNING = true; // prevent simultanious execution

                try
                {
                    if (!LOADED)
                    {
                        Assembly assembly = Assembly.GetExecutingAssembly();
                        string hatres = $"{assembly.GetName().Name}.Resources.CustomHats";
                        string[] hats = (from r in assembly.GetManifestResourceNames()
                                         where r.StartsWith(hatres) && r.EndsWith(".png")
                                         select r).ToArray<string>();

                        List<CustomHat> customhats = createCustomHatDetails(hats);
                        foreach (CustomHat ch in customhats)
                            __instance.AllHats.Add(CreateHatBehaviour(ch));
                    }
                    while (CustomHatLoader.hatdetails.Count > 0)
                    {
                        __instance.AllHats.Add(CreateHatBehaviour(CustomHatLoader.hatdetails[0]));
                        CustomHatLoader.hatdetails.RemoveAt(0);
                    }
                }
                catch (System.Exception e)
                {
                    if (!LOADED)
                        System.Console.WriteLine("Unable to add Custom Hats\n" + e);
                }
                LOADED = true;
            }
            static void Postfix(HatManager __instance)
            {
                RUNNING = false;
            }
        }

        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleAnimation))]
        private static class PlayerPhysicsHandleAnimationPatch
        {
            private static void Postfix(PlayerPhysics __instance)
            {
                AnimationClip currentAnimation = __instance.Animator.GetCurrentAnimation();
                if (currentAnimation == __instance.ClimbAnim || currentAnimation == __instance.ClimbDownAnim) return;
                HatParent hp = __instance.myPlayer.HatRenderer;
                if (hp.Hat == null) return;
                HatExtension extend = hp.Hat.getHatExtension();
                if (extend == null) return;
                if (extend.FlipImage != null)
                {
                    if (__instance.rend.flipX)
                    {
                        hp.FrontLayer.sprite = extend.FlipImage;
                    }
                    else
                    {
                        hp.FrontLayer.sprite = hp.Hat.MainImage;
                    }
                }
                if (extend.BackFlipImage != null)
                {
                    if (__instance.rend.flipX)
                    {
                        hp.BackLayer.sprite = extend.BackFlipImage;
                    }
                    else
                    {
                        hp.BackLayer.sprite = hp.Hat.BackImage;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Begin))]
        private static class ShipStatusSetHat
        {
            static void Postfix(ShipStatus __instance)
            {
                if (DestroyableSingleton<TutorialManager>.InstanceExists)
                {
                    string filePath = Path.GetDirectoryName(Application.dataPath) + @"\TheOtherHats\Test";
                    DirectoryInfo d = new DirectoryInfo(filePath);
                    string[] filePaths = d.GetFiles("*.png").Select(x => x.FullName).ToArray(); // Getting Text files
                    List<CustomHat> hats = createCustomHatDetails(filePaths, true);
                    if (hats.Count > 0)
                    {
                        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
                        {
                            var color = pc.CurrentOutfit.ColorId;
                            pc.SetHat("hat_dusk", color);
                            pc.HatRenderer.Hat = CreateHatBehaviour(hats[0], true, true);
                            pc.HatRenderer.SetHat(color);
                        }
                    }
                }
            }
        }

        private static List<TMPro.TMP_Text> hatsTabCustomTexts = new List<TMPro.TMP_Text>();
        public static string innerslothPackageName = "innerslothHats";
        private static float headerSize = 0.8f;
        private static float headerX = 0.8f;
        private static float inventoryTop = 1.5f;
        private static float inventoryBot = -2.5f;
        private static float inventoryZ = -2f;

        public static void calcItemBounds(HatsTab __instance)
        {
            inventoryTop = __instance.scroller.Inner.position.y - 0.5f;
            inventoryBot = __instance.scroller.Inner.position.y - 4.5f;
        }

        [HarmonyPatch(typeof(HatsTab), nameof(HatsTab.OnEnable))]
        public class HatsTabOnEnablePatch
        {
            public static TMPro.TMP_Text textTemplate;

            public static float createHatPackage(List<System.Tuple<HatBehaviour, HatExtension>> hats, string packageName, float YStart, HatsTab __instance)
            {
                float offset = YStart;

                if (textTemplate != null)
                {
                    TMPro.TMP_Text title = UnityEngine.Object.Instantiate<TMPro.TMP_Text>(textTemplate, __instance.scroller.Inner);
                    title.transform.parent = __instance.scroller.Inner;
                    title.transform.localPosition = new Vector3(headerX, YStart, inventoryZ);
                    title.alignment = TMPro.TextAlignmentOptions.Center;
                    title.fontSize *= 1.25f;
                    title.fontWeight = TMPro.FontWeight.Thin;
                    title.enableAutoSizing = false;
                    title.autoSizeTextContainer = true;
                    offset -= headerSize * __instance.YOffset;
                    hatsTabCustomTexts.Add(title);
                }

                var numHats = hats.Count;

                for (int i = 0; i < hats.Count; i++)
                {
                    HatBehaviour hat = hats[i].Item1;
                    HatExtension ext = hats[i].Item2;

                    float xpos = __instance.XRange.Lerp((i % __instance.NumPerRow) / (__instance.NumPerRow - 1f));
                    float ypos = offset - (i / __instance.NumPerRow) * __instance.YOffset;
                    ColorChip colorChip = UnityEngine.Object.Instantiate<ColorChip>(__instance.ColorTabPrefab, __instance.scroller.Inner);

                    int color = __instance.HasLocalPlayer() ? PlayerControl.LocalPlayer.Data.DefaultOutfit.ColorId : SaveManager.BodyColor;

                    colorChip.transform.localPosition = new Vector3(xpos, ypos, inventoryZ);
                    if (ActiveInputManager.currentControlType == ActiveInputManager.InputType.Keyboard)
                    {
                        colorChip.Button.OnMouseOver.AddListener((UnityEngine.Events.UnityAction)(() => __instance.SelectHat(hat)));
                        colorChip.Button.OnMouseOut.AddListener((UnityEngine.Events.UnityAction)(() => __instance.SelectHat(DestroyableSingleton<HatManager>.Instance.GetHatById(SaveManager.LastHat))));
                        colorChip.Button.OnClick.AddListener((UnityEngine.Events.UnityAction)(() => __instance.ClickEquip()));
                    }
                    else
                    {
                        colorChip.Button.OnClick.AddListener((UnityEngine.Events.UnityAction)(() => __instance.SelectHat(hat)));
                    }

                    colorChip.Inner.SetHat(hat, color);
                    colorChip.Inner.transform.localPosition = hat.ChipOffset;
                    colorChip.Tag = hat;
                    colorChip.Button.ClickMask = __instance.scroller.Hitbox;
                    __instance.ColorChips.Add(colorChip);
                }

                return offset - ((numHats - 1) / __instance.NumPerRow) * __instance.YOffset - headerSize;
            }

            public static bool Prefix(HatsTab __instance)
            {
                calcItemBounds(__instance);

                HatBehaviour[] unlockedHats = DestroyableSingleton<HatManager>.Instance.GetUnlockedHats();
                Dictionary<string, List<System.Tuple<HatBehaviour, HatExtension>>> packages = new Dictionary<string, List<System.Tuple<HatBehaviour, HatExtension>>>();

                Helpers.destroyList(hatsTabCustomTexts);
                Helpers.destroyList(__instance.ColorChips);

                hatsTabCustomTexts.Clear();
                __instance.ColorChips.Clear();

                textTemplate = PlayerCustomizationMenu.Instance.itemName;

                foreach (HatBehaviour hatBehaviour in unlockedHats)
                {
                    HatExtension ext = hatBehaviour.getHatExtension();

                    if (ext != null)
                    {
                        if (!packages.ContainsKey(ext.package))
                            packages[ext.package] = new List<System.Tuple<HatBehaviour, HatExtension>>();
                        packages[ext.package].Add(new System.Tuple<HatBehaviour, HatExtension>(hatBehaviour, ext));
                    }
                    else
                    {
                        if (!packages.ContainsKey(innerslothPackageName))
                            packages[innerslothPackageName] = new List<System.Tuple<HatBehaviour, HatExtension>>();
                        packages[innerslothPackageName].Add(new System.Tuple<HatBehaviour, HatExtension>(hatBehaviour, null));
                    }
                }

                float YOffset = __instance.YStart;

                var orderedKeys = packages.Keys.OrderBy((string x) =>
                {
                    if (x == innerslothPackageName) return 1000;
                    if (x == "developerHats") return 200;
                    if (x.Contains("gmEdition")) return 100;
                    if (x.Contains("shiune")) return 0;
                    return 500;
                });

                foreach (string key in orderedKeys)
                {
                    List<System.Tuple<HatBehaviour, HatExtension>> value = packages[key];
                    YOffset = createHatPackage(value, key, YOffset, __instance);
                }

                __instance.scroller.ContentYBounds.max = -(YOffset + 3.0f + headerSize);
                return false;
            }
        }

        [HarmonyPatch(typeof(HatsTab), nameof(HatsTab.Update))]
        public class HatsTabUpdatePatch
        {
            public static bool Prefix()
            {
                //return false;
                return true;
            }

            public static void Postfix(HatsTab __instance)
            {
                // Manually hide all custom TMPro.TMP_Text objects that are outside the ScrollRect
                foreach (TMPro.TMP_Text customText in hatsTabCustomTexts)
                {
                    if (customText != null && customText.transform != null && customText.gameObject != null)
                    {
                        bool active = customText.transform.position.y <= inventoryTop && customText.transform.position.y >= inventoryBot;
                        float epsilon = Mathf.Min(Mathf.Abs(customText.transform.position.y - inventoryTop), Mathf.Abs(customText.transform.position.y - inventoryBot));
                        if (active != customText.gameObject.active && epsilon > 0.1f) customText.gameObject.SetActive(active);
                    }
                }
            }
        }
    }

    public class CustomHatLoader
    {
        public static bool running = false;

        public static string[] hatRepos = new string[]
        {
            "https://raw.githubusercontent.com/yukinogatari/TheOtherHats-GM/master",
            "https://raw.githubusercontent.com/Eisbison/TheOtherHats/master"
        };

        public static List<CustomHatOnline> hatdetails = new List<CustomHatOnline>();
        private static Task hatFetchTask = null;
        public static void LaunchHatFetcher()
        {
            if (running)
                return;
            running = true;
        }

        private static string sanitizeResourcePath(string res)
        {
            if (res == null || !res.EndsWith(".png"))
                return null;

            res = res.Replace("\\", "")
                     .Replace("/", "")
                     .Replace("*", "")
                     .Replace("..", "");
            return res;
        }
        private static bool doesResourceRequireDownload(string respath, string reshash, MD5 md5)
        {
            if (reshash == null || !File.Exists(respath))
                return true;

            using (var stream = File.OpenRead(respath))
            {
                var hash = System.BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                return !reshash.Equals(hash);
            }
        }

        public class CustomHatOnline : CustomHats.CustomHat
        {
            public string reshasha { get; set; }
            public string reshashb { get; set; }
            public string reshashc { get; set; }
            public string reshashf { get; set; }
            public string reshashbf { get; set; }
        }
    }
    public static class CustomHatExtensions
    {
        public static CustomHats.HatExtension getHatExtension(this HatBehaviour hat)
        {
            CustomHats.HatExtension ret = null;
            if (CustomHats.TestExt != null && CustomHats.TestExt.condition.Equals(hat.name))
            {
                return CustomHats.TestExt;
            }
            CustomHats.CustomHatRegistry.TryGetValue(hat.name, out ret);
            return ret;
        }
    }

    [HarmonyPatch(typeof(PoolablePlayer), nameof(PoolablePlayer.UpdateFromPlayerOutfit))]
    public static class PoolablePlayerPatch
    {
        public static void Postfix(PoolablePlayer __instance)
        {
            if (__instance.VisorSlot?.transform == null || __instance.HatSlot?.transform == null) return;

            // fixes a bug in the original where the visor will show up beneath the hat,
            // instead of on top where it's supposed to be
            __instance.VisorSlot.transform.localPosition = new Vector3(
                __instance.VisorSlot.transform.localPosition.x,
                __instance.VisorSlot.transform.localPosition.y,
                __instance.HatSlot.transform.localPosition.z - 1
                );
        }
    }
}