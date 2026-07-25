namespace Aetherphone.Core.Video;

// Single source of truth for which in-game model the video plays on. For this pass there is
// exactly one target - the Carbuncle "TV" model ported from AlphaChannel (Voudi, GPL-3.0) - and
// every consumer (companion tracking, the resource hooks, Penumbra staging) reads through this
// class rather than repeating the game paths. A later pass adding a dropdown over several
// minions should only need to change what this class returns, not the render path itself.
internal static class VideoRenderTarget
{
    // NPC base ID for Carbuncle, the companion model repurposed as a screen. BattleNpc-kind
    // objects (Carbuncle happens to be one, unlike ordinary minions) report BaseId through
    // BNpcBase's own ModelChara link, not directly against a sheet row - 13498 is that resolved
    // value. A Plush-Cushion-targeted pass (BaseId 66, ObjectKind.Companion, a third-party TV
    // skin mod) was tried and reverted - back to Carbuncle itself as the "original TV".
    internal const uint CompanionBaseId = 13498;

    internal const int ScreenWidth = 1920;
    internal const int ScreenHeight = 1080;

    // The texture path baked into the bundled .avfx's own compiled internal structure, fixed at
    // whatever model the asset's author (AlphaChannel/Voudi) built it against. It has nothing to
    // do with which model the VFX gets invoked on (that's ScreenVfxGamePathTemplate below, which
    // we do get to name) - no matter what model plays the VFX, the VFX itself always requests
    // this exact texture path internally.
    internal const string ScreenTextureGamePath =
        "chara/monster/m7002/obj/body/b0001/vfx/texture/alphachannelscreentex.atex";

    // Substring the resource-load hook watches for on every resource request.
    internal const string ScreenTextureGamePathFragment =
        "chara/monster/m7002/obj/body/b0001/vfx/texture/alphachannelscreentex";

    // Outer VFX invocation path - ours to name, since we both invoke it and register its
    // Penumbra redirect. {0} is a per-session id so concurrent instances, and AlphaChannel
    // running alongside, never collide on the same game path.
    internal const string ScreenVfxGamePathTemplate =
        "chara/monster/m7002/obj/body/b0001/vfx/texture/aetherstreamscreen_{0}.avfx";

    internal const string ScreenTextureBundledFile = "alphachannelscreentex.atex";
    internal const string ScreenVfxBundledFile = "aetherstreamscreen.avfx";

    // Ported from AlphaChannel's Resources.LoadPenumbraModResources (Voudi, GPL-3.0) minus the QR
    // watch-texture entry, which belongs to the sync layer we are not porting. The active table
    // ApplyCompanionAppearance reads - back to Carbuncle's own m7002 paths as the "original TV".
    internal static readonly (string GamePath, string BundledFile)[] CompanionModFiles =
    {
        ("chara/monster/m7002/animation/a0001/bt_common/resident/monster.pap", "carbuncle/monster.pap"),
        ("chara/monster/m7002/obj/body/b0001/material/v0001/mt_m7002b0001_a.mtrl", "carbuncle/mt_m7002b0001_a.mtrl"),
        ("chara/monster/m7002/obj/body/b0001/material/v0002/mt_m7002b0001_a.mtrl", "carbuncle/mt_m7002b0001_a.mtrl"),
        ("chara/monster/m7002/obj/body/b0001/material/v0003/mt_m7002b0001_a.mtrl", "carbuncle/mt_m7002b0001_a.mtrl"),
        ("chara/monster/m7002/obj/body/b0001/material/v0004/mt_m7002b0001_a.mtrl", "carbuncle/mt_m7002b0001_a.mtrl"),
        ("chara/monster/m7002/obj/body/b0001/material/v0005/mt_m7002b0001_a.mtrl", "carbuncle/mt_m7002b0001_a.mtrl"),
        ("chara/monster/m7002/obj/body/b0001/material/v0006/mt_m7002b0001_a.mtrl", "carbuncle/mt_m7002b0001_a.mtrl"),
        ("chara/monster/m7002/obj/body/b0001/material/v0001/mt_m7002b0001_b.mtrl", "carbuncle/mt_m7002b0001_b.mtrl"),
        ("chara/monster/m7002/obj/body/b0001/material/v0002/mt_m7002b0001_b.mtrl", "carbuncle/mt_m7002b0001_b.mtrl"),
        ("chara/monster/m7002/obj/body/b0001/material/v0003/mt_m7002b0001_b.mtrl", "carbuncle/mt_m7002b0001_b.mtrl"),
        ("chara/monster/m7002/obj/body/b0001/material/v0004/mt_m7002b0001_b.mtrl", "carbuncle/mt_m7002b0001_b.mtrl"),
        ("chara/monster/m7002/obj/body/b0001/material/v0005/mt_m7002b0001_b.mtrl", "carbuncle/mt_m7002b0001_b.mtrl"),
        ("chara/monster/m7002/obj/body/b0001/material/v0006/mt_m7002b0001_b.mtrl", "carbuncle/mt_m7002b0001_b.mtrl"),
        ("chara/monster/m7002/obj/body/b0001/model/m7002b0001.mdl", "carbuncle/m7002b0001.mdl"),
        ("chara/monster/m7002/obj/body/b0001/texture/tv_id.tex", "carbuncle/tv_id.tex"),
        ("chara/monster/m7002/obj/body/b0001/texture/tv_n.tex", "carbuncle/tv_n.tex"),
        ("chara/monster/m7002/obj/body/b0001/texture/tv_d.tex", "carbuncle/tv_d.tex"),
        ("chara/monster/m7002/obj/body/b0001/texture/tv_s.tex", "carbuncle/tv_id.tex"),
        ("chara/monster/m7002/obj/body/b0001/vfx/texture/flas001bt.atex", "carbuncle/flas001bt.atex"),
        ("chara/monster/m7002/obj/body/b0001/vfx/texture/pk_x001a_h.atex", "carbuncle/pk_x001a_h.atex"),
        ("chara/monster/m7002/obj/body/b0001/vfx/texture/glow002bf.atex", "carbuncle/glow002bf.atex"),
        ("chara/monster/m7002/obj/body/b0001/vfx/texture/flas001ct.atex", "carbuncle/flas001bt.atex"),
        ("chara/monster/m7002/obj/body/b0001/texture/unknown_b_id_1400445740.tex", "carbuncle/tv_id.tex"),
        ("chara/monster/m7002/obj/body/b0001/texture/unknown_b_n_1400445740.tex", "carbuncle/tv_n.tex"),
        ("chara/monster/m7002/obj/body/b0001/texture/unknown_b_s_1400445740.tex", "carbuncle/tv_id.tex"),
    };

    // Experimental: the same bundled Carbuncle TV model/material/texture files as
    // CompanionModFiles, but redirected into the Plush Cushion's own game-path slot (m8058)
    // instead of Carbuncle's (m7002), per your call to try reusing it rather than waiting on a
    // model authored specifically for 8058. The GAME PATH (left column, where Penumbra intercepts
    // the request) uses m8058 throughout, matching where the game actually looks when rendering a
    // Plush Cushion. The bundled FILE (right column) is unchanged - still the Carbuncle .mdl/.mtrl
    // content, whose own internal material references are still literally "mt_m7002b0001_*",
    // baked into that binary and unchangeable from here.
    //
    // Real risk: this model was exported against Carbuncle's own skeleton. Whether the game
    // accepts it in a Plush Cushion's rig - correctly, T-posed, corrupted, or not at all - is
    // unverified; there's no way to know short of testing it live.
    internal static readonly (string GamePath, string BundledFile)[] PlushCushionModFiles =
    {
        ("chara/monster/m8058/animation/a0001/bt_common/resident/monster.pap", "carbuncle/monster.pap"),
        ("chara/monster/m8058/obj/body/b0001/material/v0001/mt_m7002b0001_a.mtrl", "carbuncle/mt_m7002b0001_a.mtrl"),
        ("chara/monster/m8058/obj/body/b0001/material/v0002/mt_m7002b0001_a.mtrl", "carbuncle/mt_m7002b0001_a.mtrl"),
        ("chara/monster/m8058/obj/body/b0001/material/v0003/mt_m7002b0001_a.mtrl", "carbuncle/mt_m7002b0001_a.mtrl"),
        ("chara/monster/m8058/obj/body/b0001/material/v0004/mt_m7002b0001_a.mtrl", "carbuncle/mt_m7002b0001_a.mtrl"),
        ("chara/monster/m8058/obj/body/b0001/material/v0005/mt_m7002b0001_a.mtrl", "carbuncle/mt_m7002b0001_a.mtrl"),
        ("chara/monster/m8058/obj/body/b0001/material/v0006/mt_m7002b0001_a.mtrl", "carbuncle/mt_m7002b0001_a.mtrl"),
        ("chara/monster/m8058/obj/body/b0001/material/v0001/mt_m7002b0001_b.mtrl", "carbuncle/mt_m7002b0001_b.mtrl"),
        ("chara/monster/m8058/obj/body/b0001/material/v0002/mt_m7002b0001_b.mtrl", "carbuncle/mt_m7002b0001_b.mtrl"),
        ("chara/monster/m8058/obj/body/b0001/material/v0003/mt_m7002b0001_b.mtrl", "carbuncle/mt_m7002b0001_b.mtrl"),
        ("chara/monster/m8058/obj/body/b0001/material/v0004/mt_m7002b0001_b.mtrl", "carbuncle/mt_m7002b0001_b.mtrl"),
        ("chara/monster/m8058/obj/body/b0001/material/v0005/mt_m7002b0001_b.mtrl", "carbuncle/mt_m7002b0001_b.mtrl"),
        ("chara/monster/m8058/obj/body/b0001/material/v0006/mt_m7002b0001_b.mtrl", "carbuncle/mt_m7002b0001_b.mtrl"),
        ("chara/monster/m8058/obj/body/b0001/model/m8058b0001.mdl", "carbuncle/m7002b0001.mdl"),
        ("chara/monster/m8058/obj/body/b0001/texture/tv_id.tex", "carbuncle/tv_id.tex"),
        ("chara/monster/m8058/obj/body/b0001/texture/tv_n.tex", "carbuncle/tv_n.tex"),
        ("chara/monster/m8058/obj/body/b0001/texture/tv_d.tex", "carbuncle/tv_d.tex"),
        ("chara/monster/m8058/obj/body/b0001/texture/tv_s.tex", "carbuncle/tv_id.tex"),
        ("chara/monster/m8058/obj/body/b0001/vfx/texture/flas001bt.atex", "carbuncle/flas001bt.atex"),
        ("chara/monster/m8058/obj/body/b0001/vfx/texture/pk_x001a_h.atex", "carbuncle/pk_x001a_h.atex"),
        ("chara/monster/m8058/obj/body/b0001/vfx/texture/glow002bf.atex", "carbuncle/glow002bf.atex"),
        ("chara/monster/m8058/obj/body/b0001/vfx/texture/flas001ct.atex", "carbuncle/flas001bt.atex"),
        ("chara/monster/m8058/obj/body/b0001/texture/unknown_b_id_1400445740.tex", "carbuncle/tv_id.tex"),
        ("chara/monster/m8058/obj/body/b0001/texture/unknown_b_n_1400445740.tex", "carbuncle/tv_n.tex"),
        ("chara/monster/m8058/obj/body/b0001/texture/unknown_b_s_1400445740.tex", "carbuncle/tv_id.tex"),
    };
}
