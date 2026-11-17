namespace LightCrosshair.Core;

internal static class KnownGames
{
    // Offline suggestions only; executable names are never sent to a network service.
    public static readonly IReadOnlyDictionary<string, string> Entries = new Dictionary<string, string>
    {
        ["cs2.exe"] = "Counter-Strike 2", ["valorant-win64-shipping.exe"] = "VALORANT", ["r5apex.exe"] = "Apex Legends",
        ["fortniteclient-win64-shipping.exe"] = "Fortnite", ["overwatch.exe"] = "Overwatch", ["destiny2.exe"] = "Destiny 2",
        ["rainbowsix.exe"] = "Rainbow Six Siege", ["rainbowsix_vulkan.exe"] = "Rainbow Six Siege", ["cod.exe"] = "Call of Duty",
        ["pubg.exe"] = "PUBG", ["tslgame.exe"] = "PUBG", ["escapefromtarkov.exe"] = "Escape from Tarkov",
        ["rustclient.exe"] = "Rust", ["dayz_x64.exe"] = "DayZ", ["arma3_x64.exe"] = "Arma 3",
        ["helldivers2.exe"] = "Helldivers 2", ["halo infinite.exe"] = "Halo Infinite", ["haloinfinite.exe"] = "Halo Infinite",
        ["bf2042.exe"] = "Battlefield 2042", ["bfv.exe"] = "Battlefield V", ["bf1.exe"] = "Battlefield 1",
        ["doom.exe"] = "DOOM", ["doometernalx64vk.exe"] = "DOOM Eternal", ["quakechampions.exe"] = "Quake Champions",
        ["quake3.exe"] = "Quake III Arena", ["hl2.exe"] = "Half-Life 2", ["left4dead2.exe"] = "Left 4 Dead 2",
        ["tf_win64.exe"] = "Team Fortress 2", ["gta5.exe"] = "Grand Theft Auto V", ["rdr2.exe"] = "Red Dead Redemption 2",
        ["cyberpunk2077.exe"] = "Cyberpunk 2077", ["starfield.exe"] = "Starfield", ["fallout4.exe"] = "Fallout 4",
        ["borderlands3.exe"] = "Borderlands 3", ["borderlands2.exe"] = "Borderlands 2", ["warframe.x64.exe"] = "Warframe",
        ["warframe.exe"] = "Warframe", ["remnant2-win64-shipping.exe"] = "Remnant II", ["gunfire reborn.exe"] = "Gunfire Reborn",
        ["drlg.exe"] = "Deep Rock Galactic", ["fsd-win64-shipping.exe"] = "Deep Rock Galactic", ["readyornot-win64-shipping.exe"] = "Ready or Not",
        ["squadgame.exe"] = "Squad", ["insurgencyclient-win64-shipping.exe"] = "Insurgency: Sandstorm", ["huntgame.exe"] = "Hunt: Showdown",
        ["seaoftheives.exe"] = "Sea of Thieves", ["sotgame.exe"] = "Sea of Thieves", ["robloxplayerbeta.exe"] = "Roblox",
        ["unturned.exe"] = "Unturned", ["7daystodie.exe"] = "7 Days to Die", ["valheim.exe"] = "Valheim",
        ["palworld-win64-shipping.exe"] = "Palworld", ["arkascended.exe"] = "ARK: Survival Ascended"
    };
}
