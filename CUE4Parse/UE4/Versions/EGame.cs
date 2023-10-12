using System.Runtime.CompilerServices;

namespace CUE4Parse.UE4.Versions {
    public enum EGame {
        // improved version based on Cuddle
        // 32-bits
        // 00000000 00000000 X0000000 00000000
        // ue_major ue_minor ue_game  ue_game
        // X = Bit flag for if it's an official branch.
        // very unlikely that versions will ever go over 255, but more likely that games go over 255 given enough time.
        // written format: MAJOR.MINOR.GAME
        // max: 255.255.32767
        // special versions:
        //      4.25.32768 = 4.25.plus = Plus PS5/XSX support
        //      4.27.32768 = 4.27.plus = Plus Ray Tracing support
        //      5.0.32768 = 5.0.16678002 = Pre-IoStore Refactor
        //
        // old GAME_StarWarsJediSurvivor = 0x10001a7 = 0x1, 0x1a, 0x7 = UE4.26 Game 7
        // new GAME_StarWarsJediSurvivor = 0x41a0007 = 0x4, 0x1a, 0b0, 0x7 = UE4.26 Game 7, Flag 0
        // old GAME_UE4_25_Plus = 0x1000191 = 0x1, 0x19, 0x1 = UE4.25 Game 1
        // new GAME_UE4_25_Plus = 0x4198000 = 0x4, 0x19, 0b1, 0x0 = UE4.25 Game 0, Flag 1
        GAME_UE4_0 = 4 << 24,
        GAME_UE4_1 = GAME_UE4_0 + (1 << 16),
        GAME_UE4_2 = GAME_UE4_0 + (2 << 16),
        GAME_UE4_3 = GAME_UE4_0 + (3 << 16),
        GAME_UE4_4 = GAME_UE4_0 + (4 << 16),
        GAME_UE4_5 = GAME_UE4_0 + (5 << 16),
            GAME_ArkSurvivalEvolved = GAME_UE4_5 + 1,
        GAME_UE4_6 = GAME_UE4_0 + (6 << 16),
        GAME_UE4_7 = GAME_UE4_0 + (7 << 16),
        GAME_UE4_8 = GAME_UE4_0 + (8 << 16),
        GAME_UE4_9 = GAME_UE4_0 + (9 << 16),
        GAME_UE4_10 = GAME_UE4_0 + (10 << 16),
            GAME_SeaOfThieves = GAME_UE4_10 + 1,
        GAME_UE4_11 = GAME_UE4_0 + (11 << 16),
            GAME_GearsOfWar4 = GAME_UE4_11 + 1,
        GAME_UE4_12 = GAME_UE4_0 + (12 << 16),
        GAME_UE4_13 = GAME_UE4_0 + (13 << 16),
            GAME_StateOfDecay2 = GAME_UE4_13 + 1,
        GAME_UE4_14 = GAME_UE4_0 + (14 << 16),
            GAME_TEKKEN7 = GAME_UE4_14 + 1,
        GAME_UE4_15 = GAME_UE4_0 + (15 << 16),
        GAME_UE4_16 = GAME_UE4_0 + (16 << 16),
            GAME_PlayerUnknownsBattlegrounds = GAME_UE4_16 + 1,
            GAME_TrainSimWorld2020 = GAME_UE4_16 + 2,
        GAME_UE4_17 = GAME_UE4_0 + (17 << 16),
            GAME_AWayOut = GAME_UE4_17 + 1,
        GAME_UE4_18 = GAME_UE4_0 + (18 << 16),
            GAME_KingdomHearts3 = GAME_UE4_18 + 1,
            GAME_FinalFantasy7Remake = GAME_UE4_18 + 2,
            GAME_AceCombat7 = GAME_UE4_18 + 3,
        GAME_UE4_19 = GAME_UE4_0 + (19 << 16),
            GAME_Paragon = GAME_UE4_19 + 1,
        GAME_UE4_20 = GAME_UE4_0 + (20 << 16),
            GAME_Borderlands3 = GAME_UE4_20 + 1,
        GAME_UE4_21 = GAME_UE4_0 + (21 << 16),
            GAME_StarWarsJediFallenOrder = GAME_UE4_21 + 1,
        GAME_UE4_22 = GAME_UE4_0 + (22 << 16),
        GAME_UE4_23 = GAME_UE4_0 + (23 << 16),
            GAME_ApexLegendsMobile = GAME_UE4_23 + 1,
        GAME_UE4_24 = GAME_UE4_0 + (24 << 16),
        GAME_UE4_25 = GAME_UE4_0 + (25 << 16),
            GAME_RogueCompany = GAME_UE4_25 + 1,
            GAME_DeadIsland2 = GAME_UE4_25 + 2,
            GAME_KenaBridgeofSpirits = GAME_UE4_25 + 3,
            GAME_CalabiYau = GAME_UE4_25 + 4,
            GAME_SYNCED = GAME_UE4_25 + 5,
            GAME_UE4_25_Plus = GAME_UE4_25 + (1 << 15),
        GAME_UE4_26 = GAME_UE4_0 + (26 << 16),
            GAME_GTATheTrilogyDefinitiveEdition = GAME_UE4_26 + 1,
            GAME_ReadyOrNot = GAME_UE4_26 + 2,
            GAME_BladeAndSoul = GAME_UE4_26 + 3,
            GAME_TowerOfFantasy = GAME_UE4_26 + 4,
            GAME_Dauntless = GAME_UE4_26 + 5,
            GAME_TheDivisionResurgence = GAME_UE4_26 + 6,
            GAME_StarWarsJediSurvivor = GAME_UE4_26 + 7,
            GAME_Snowbreak = GAME_UE4_26 + 8,
            GAME_TorchlightInfinite = GAME_UE4_26 + 9,
        GAME_UE4_27 = GAME_UE4_0 + (27 << 16),
            GAME_Splitgate = GAME_UE4_27 + 1,
            GAME_HYENAS = GAME_UE4_27 + 2,
            GAME_HogwartsLegacy = GAME_UE4_27 + 3,
            GAME_OutlastTrials = GAME_UE4_27 + 4,
            GAME_Gollum = GAME_UE4_27 + 5,
            GAME_Grounded = GAME_UE4_27 + 6,
            GAME_UE4_27_Plus = GAME_UE4_27 + (1 << 15),
        GAME_UE4_28 = GAME_UE4_0 + (28 << 16),

        GAME_UE4_LATEST = GAME_UE4_28,

        // TODO Figure out the enum name for UE5 Early Access
        // The commit https://github.com/EpicGames/UnrealEngine/commit/cf116088ae6b65c1701eee99288e43c7310d6bb1#diff-6178e9d97c98e321fc3f53770109ea7f6a8ea7a86cac542717a81922f2f93613R723
        // changed the IoStore and its packages format which breaks backward compatibility with 5.0.0-16433597+++UE5+Release-5.0-EarlyAccess
        GAME_UE5_0 = 5 << 24,
            GAME_MeetYourMaker = GAME_UE5_0 + 1,
        GAME_UE5_0_IoStoreRefactor = GAME_UE5_0 + (1 << 15),
        GAME_UE5_1 = GAME_UE5_0 + (1 << 16),
            GAME_CarnalInstinct = GAME_UE5_1 + 1,
        GAME_UE5_2 = GAME_UE5_0 + (2 << 16),
        GAME_UE5_3 = GAME_UE5_0 + (3 << 16),
        GAME_UE5_4 = GAME_UE5_0 + (4 << 16),

        GAME_UE5_LATEST = GAME_UE5_4,

        // Valorant's wild version changes.
        GAME_Valorant_Alpha = GAME_UE4_22 + 100, // valorant 0.45 - 0.47 - ares, closed beta
        GAME_Valorant_Beta = GAME_UE4_23 + 100, // valorant 0.48 - 0.50 - cc beta
        GAME_Valorant_EP1 = GAME_UE4_24 + 100,
        GAME_Valorant_EP2 = GAME_UE4_25 + 100,
        GAME_Valorant_EP3 = GAME_Valorant_EP2 + 1,
        GAME_Valorant_EP4 = GAME_UE4_26 + 100,
        GAME_Valorant_EP5 = GAME_Valorant_EP4 + 1,
        GAME_Valorant_EP6 = GAME_UE4_27 + 100,
        GAME_Valorant = GAME_Valorant_EP6,
    }

    public enum EValorantGame {
        NotValorant,
        Alpha,
        Beta,
        EP1,
        EP2,
        EP3,
        EP4,
        EP5,
        EP6,
    }

    public static class GameUtils {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GAME_UE4(int x) {
            return (int)(EGame.GAME_UE4_0 + (x << 16));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GAME_UE5(int x) {
            return (int)(EGame.GAME_UE5_0 + (x << 16));
        }

        public static EValorantGame GetValorantVersion(this EGame game) {
            return game switch {
                       EGame.GAME_Valorant_Alpha => EValorantGame.Alpha,
                       EGame.GAME_Valorant_Beta  => EValorantGame.Beta,
                       EGame.GAME_Valorant_EP1   => EValorantGame.EP1,
                       EGame.GAME_Valorant_EP2   => EValorantGame.EP2,
                       EGame.GAME_Valorant_EP3   => EValorantGame.EP3,
                       EGame.GAME_Valorant_EP4   => EValorantGame.EP4,
                       EGame.GAME_Valorant_EP5   => EValorantGame.EP5,
                       EGame.GAME_Valorant_EP6   => EValorantGame.EP6,
                       _                         => EValorantGame.NotValorant,
                   };
        }

        public static FPackageFileVersion GetVersion(this EGame game) {
            // Custom UE Games
            // If a game needs a even more specific custom version than the major release version you can add it below
            // if (game == EGame.GAME_VALORANT)
            //     return UE4Version.VER_UE4_24;

            if (game >= EGame.GAME_UE5_0) {
                return game switch {
                    < EGame.GAME_UE5_1 => new FPackageFileVersion(522, 1004),
                    < EGame.GAME_UE5_2 => new FPackageFileVersion(522, 1008),
                    < EGame.GAME_UE5_4 => new FPackageFileVersion(522, 1009),
                    _ => new FPackageFileVersion((int) EUnrealEngineObjectUE4Version.AUTOMATIC_VERSION, (int) EUnrealEngineObjectUE5Version.AUTOMATIC_VERSION),
                };
            }

            return FPackageFileVersion.CreateUE4Version(game switch {
                // General UE4 Versions
                < EGame.GAME_UE4_1 => 342,
                < EGame.GAME_UE4_2 => 352,
                < EGame.GAME_UE4_3 => 363,
                < EGame.GAME_UE4_4 => 382,
                < EGame.GAME_UE4_5 => 385,
                < EGame.GAME_UE4_6 => 401,
                < EGame.GAME_UE4_7 => 413,
                < EGame.GAME_UE4_8 => 434,
                < EGame.GAME_UE4_9 => 451,
                < EGame.GAME_UE4_10 => 482,
                < EGame.GAME_UE4_11 => 482,
                < EGame.GAME_UE4_12 => 498,
                < EGame.GAME_UE4_13 => 504,
                < EGame.GAME_UE4_14 => 505,
                < EGame.GAME_UE4_15 => 508,
                < EGame.GAME_UE4_16 => 510,
                < EGame.GAME_UE4_17 => 513,
                < EGame.GAME_UE4_18 => 513,
                < EGame.GAME_UE4_19 => 514,
                < EGame.GAME_UE4_20 => 516,
                < EGame.GAME_UE4_21 => 516,
                < EGame.GAME_UE4_22 => 517,
                < EGame.GAME_UE4_23 => 517,
                < EGame.GAME_UE4_24 => 517,
                < EGame.GAME_UE4_25 => 518,
                < EGame.GAME_UE4_26 => 518,
                < EGame.GAME_UE4_27 => 522,
                _ => (int) EUnrealEngineObjectUE4Version.AUTOMATIC_VERSION,
            });
        }
    }
}
