using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hazel;
using TownOfHost.Roles.Core;

namespace TownOfHost.Modules;

static class NameManager
{
    public static Dictionary<byte, bool> NotifyDirty = new(15);

    //Name作成用StingBuilder群
    private static StringBuilder Mark = new(20);
    private static StringBuilder Lower = new(120);
    private static StringBuilder Suffix = new(120);

    public static void SetNameForSeer(PlayerControl seer, bool isForMeeting)
    {
        if (!seer.AmOwner)
        {
            //ホスト以外は他のseerを見る必要がない
            if (!AmongUsClient.Instance.AmHost)
            {
                NotifyDirty[seer.PlayerId] = false;
            }
        }
        //更新する必要がなければここで終わり
        if (!NotifyDirty[seer.PlayerId]) return;

        var nameSender = !seer.IsModClient() ? CustomRpcSender.Create($"NameSender for {seer.name}", SendOption.Reliable) : null;
        var seerRole = seer.GetRoleClass();

        var isMushroomMixupActive = Utils.IsActive(SystemTypes.MushroomMixupSabotage);
        var isCamouflage = Utils.IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool() && !seer.Is(CustomRoleTypes.Impostor);

        foreach (var seen in Main.AllPlayerControls)
        {
            var seenRoleData = Utils.GetRoleNameAndProgressTextData(seer, seen);
            var NoCache = false;
            string RealName;
            Mark.Clear();
            Lower.Clear();
            Suffix.Clear();

            //名前変更
            RealName = seen.GetRealName(isForMeeting);

            //NameColorManager準拠の処理
            RealName = RealName.ApplyNameColorData(seer, seen, isForMeeting);

            //seer役職が対象のMark
            Mark.Append(seerRole?.GetMark(seer, seen, isForMeeting));
            //seerに関わらず発動するMark
            Mark.Append(CustomRoleManager.GetMarkOthers(seer, seen, isForMeeting));

            if (seen.Is(CustomRoles.Lovers))
            {
                if (seer.Is(CustomRoles.Lovers) || !seer.IsAlive())
                {
                    Mark.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Lovers)}>♡</color>");
                }
            }

            //seer役職が対象のSuffix
            Suffix.Append(seerRole?.GetSuffix(seer, seen, isForMeeting));

            //seerに関わらず発動するSuffix
            Suffix.Append(CustomRoleManager.GetSuffixOthers(seer, seen, isForMeeting));

            if (isCamouflage && seer != seen && !isForMeeting)
                RealName = $"<size=0>{RealName}</size> ";

            string DeathReason = seer.KnowDeathReason(seen) ? $"({Utils.ColorString(Utils.GetRoleColor(CustomRoles.Doctor), Utils.GetVitalText(seen.PlayerId))})" : "";

            //seer役職が対象のLowerText
            Lower.Append(seerRole?.GetLowerText(seer, seen, isForMeeting));
            //seerに関わらず発動するLowerText
            Lower.Append(CustomRoleManager.GetLowerTextOthers(seer, seen, isForMeeting));

            string fontSize = isForMeeting ? "1.5" : Main.RoleTextSize.ToString();
            var seenRoleText = seenRoleData.enabled ? $"<size={fontSize}>{seenRoleData.text}</size>\r\n" : "";
            var newName = $"{seenRoleText}{RealName}{DeathReason}{Mark}";

            var suffixtext = Lower.ToString() + Suffix.ToString();
            if (suffixtext != "")
            {
                newName += $"\r\n{suffixtext}";
            }

            if (seer.AmOwner)
            {
                //役職テキストの非表示
                var RoleTextTransform = seen.cosmetics.nameText.transform.Find("RoleText");
                var RoleText = RoleTextTransform?.GetComponent<TMPro.TextMeshPro>();
                if (RoleText != null)
                    RoleText.enabled = false;

                seen.cosmetics.nameText.text = newName;
            }
            else
            {
                if (!NoCache && (Main.LastNotifyNames.TryGetValue((seer.PlayerId, seen.PlayerId), out var lastNotifyName) && lastNotifyName == newName)) continue;
                Main.LastNotifyNames[(seer.PlayerId, seen.PlayerId)] = newName;
                nameSender?.RpcSetName(seen, newName, seer);
            }
        }
        nameSender?.SendMessage();
        NotifyDirty[seer.PlayerId] = false;
    }
}
