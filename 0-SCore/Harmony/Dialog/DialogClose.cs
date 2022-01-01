using HarmonyLib;

namespace Harmony.Dialog
{

    // Removes the custom IsBusy bool, which pauses custom NPCs in their activities, allowing the player to talk to them.
    [HarmonyPatch(typeof(XUiC_DialogWindowGroup))]
    [HarmonyPatch("OnClose")]
    public class OnClose
    {
        public static bool Prefix(XUiC_DialogWindowGroup __instance)
        {
            if (!__instance.xui.playerUI.entityPlayer.Buffs.HasCustomVar("CurrentNPC")) return true;
            var entityID = (int)__instance.xui.playerUI.entityPlayer.Buffs.GetCustomVar("CurrentNPC");
            var myEntity = __instance.xui.playerUI.entityPlayer.world.GetEntity(entityID) as global::EntityAlive;
            if (myEntity != null)
            {
                myEntity.Buffs.RemoveCustomVar("CurrentPlayer");
                myEntity.emodel.avatarController.SetBool("IsBusy", false);

            }
            __instance.xui.playerUI.entityPlayer.Buffs.RemoveCustomVar("CurrentNPC");

            return true;
        }
    }

    // Removes the custom IsBusy bool, which pauses custom NPCs in their activities, allowing the player to talk to them.
    [HarmonyPatch(typeof(XUiC_DialogWindowGroup))]
    [HarmonyPatch("OnOpen")]
    public class OnOpen
    {
        public static bool Prefix(XUiC_DialogWindowGroup __instance)
        {
            if (!__instance.xui.playerUI.entityPlayer.Buffs.HasCustomVar("CurrentNPC")) return true;
            var entityID = (int)__instance.xui.playerUI.entityPlayer.Buffs.GetCustomVar("CurrentNPC");
            var myEntity = __instance.xui.playerUI.entityPlayer.world.GetEntity(entityID) as global::EntityAlive;
            if (myEntity != null)
            {

                myEntity.emodel.avatarController.SetBool("IsBusy", true);
                myEntity.RotateTo(__instance.xui.playerUI.entityPlayer, 30f, 30f);
                myEntity.SetLookPosition(__instance.xui.playerUI.entityPlayer.getHeadPosition());
                EntityUtilities.Stop(entityID);
            }

            return true;
        }
    }
}