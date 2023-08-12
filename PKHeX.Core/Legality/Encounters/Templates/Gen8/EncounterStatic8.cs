using static PKHeX.Core.OverworldCorrelation8Requirement;

namespace PKHeX.Core;

/// <summary>
/// Generation 8 Static Encounter
/// </summary>
public sealed record EncounterStatic8(GameVersion Version = GameVersion.SWSH)
    : IEncounterable, IEncounterMatch, IEncounterConvertible<PK8>, IMoveset, IRelearn,
        IFlawlessIVCount, IFixedGender, IDynamaxLevelReadOnly, IGigantamaxReadOnly, IOverworldCorrelation8, IFatefulEncounterReadOnly
{
    public int Generation => 8;
    public EntityContext Context => EntityContext.Gen8;
    int ILocation.Location => Location;
    public bool Gift => FixedBall != Ball.None;
    public bool IsShiny => Shiny == Shiny.Always;
    public bool EggEncounter => false;
    int ILocation.EggLocation => 0;

    public required ushort Location { get; init; }
    public required ushort Species { get; init; }
    public byte Form { get; init; }
    public required byte Level { get; init; }
    public Moveset Moves { get; init; }
    public Moveset Relearn { get; init; }
    public IndividualValueSet IVs { get; init; }
    public Crossover8 Crossover { get; init; }
    public AreaWeather8 Weather { get; init; } = AreaWeather8.Normal;
    public byte DynamaxLevel { get; init; }
    public Nature Nature { get; init; }
    public Shiny Shiny { get; init; }
    public AbilityPermission Ability { get; init; }
    public sbyte Gender { get; init; } = -1;
    public Ball FixedBall { get; init; }
    public byte FlawlessIVCount { get; init; }
    public bool ScriptedNoMarks { get; init; }
    public bool CanGigantamax { get; init; }
    public bool FatefulEncounter { get; init; }

    public string Name => "Static Encounter";
    public string LongName => Name;
    public byte LevelMin => Level;
    public byte LevelMax => Level;

    public bool IsOverworldCorrelation
    {
        get
        {
            if (Gift)
                return false; // gifts can have any 128bit seed from overworld
            if (ScriptedNoMarks)
                return false;  // scripted encounters don't act as saved spawned overworld encounters
            return true;
        }
    }

    public OverworldCorrelation8Requirement GetRequirement(PKM pk) => IsOverworldCorrelation
        ? MustHave
        : MustNotHave;

    public bool IsOverworldCorrelationCorrect(PKM pk)
    {
        return Overworld8RNG.ValidateOverworldEncounter(pk, Shiny == Shiny.Random ? Shiny.FixedValue : Shiny, FlawlessIVCount);
    }

    #region Generating

    PKM IEncounterConvertible.ConvertToPKM(ITrainerInfo tr) => ConvertToPKM(tr);
    PKM IEncounterConvertible.ConvertToPKM(ITrainerInfo tr, EncounterCriteria criteria) => ConvertToPKM(tr, criteria);

    public PK8 ConvertToPKM(ITrainerInfo tr) => ConvertToPKM(tr, EncounterCriteria.Unrestricted);

    public PK8 ConvertToPKM(ITrainerInfo tr, EncounterCriteria criteria)
    {
        var version = this.GetCompatibleVersion((GameVersion)tr.Game);
        int lang = (int)Language.GetSafeLanguage(Generation, (LanguageID)tr.Language, version);
        var pk = new PK8
        {
            Species = Species,
            CurrentLevel = Level,
            Met_Location = Location,
            Met_Level = Level,
            MetDate = EncounterDate.GetDateSwitch(),
            Ball = (byte)(FixedBall != Ball.None ? FixedBall : Ball.Poke),
            FatefulEncounter = FatefulEncounter,

            ID32 = tr.ID32,
            Version = (byte)version,
            Language = lang,
            OT_Gender = tr.Gender,
            OT_Name = tr.OT,
            OT_Friendship = PersonalTable.SWSH[Species, Form].BaseFriendship,

            Nickname = SpeciesName.GetSpeciesNameGeneration(Species, lang, Generation),
            HeightScalar = PokeSizeUtil.GetRandomScalar(),
            WeightScalar = PokeSizeUtil.GetRandomScalar(),

            DynamaxLevel = DynamaxLevel,
            CanGigantamax = CanGigantamax,
        };

        SetPINGA(pk, criteria);

        EncounterUtil1.SetEncounterMoves(pk, version, Level);
        pk.ResetPartyStats();

        return pk;
    }

    private void SetPINGA(PK8 pk, EncounterCriteria criteria)
    {
        if (Weather is AreaWeather8.Heavy_Fog && EncounterArea8.IsBoostedArea60Fog(Location))
            pk.CurrentLevel = pk.Met_Level = EncounterArea8.BoostLevel;

        var req = GetRequirement(pk);
        if (req != MustHave)
        {
            pk.EncryptionConstant = Util.Rand32();
            return;
        }
        var shiny = Shiny == Shiny.Random ? Shiny.FixedValue : Shiny;
        Overworld8RNG.ApplyDetails(pk, criteria, shiny, FlawlessIVCount);
    }

    #endregion

    #region Matching

    public bool IsMatchExact(PKM pk, EvoCriteria evo)
    {
        if (!IsMatchLevel(pk))
            return false;
        if (!IsMatchLocation(pk))
            return false;
        if (!IsMatchEggLocation(pk))
            return false;
        if (pk is PK8 d && d.DynamaxLevel < DynamaxLevel)
            return false;
        if (pk.Met_Level < EncounterArea8.BoostLevel && Weather is AreaWeather8.Heavy_Fog && EncounterArea8.IsBoostedArea60Fog(Location))
            return false;
        if (Gender != -1 && pk.Gender != Gender)
            return false;
        if (Form != evo.Form && !FormInfo.IsFormChangeable(Species, Form, pk.Form, Context, pk.Context))
            return false;
        if (IVs.IsSpecified && !Legal.GetIsFixedIVSequenceValidSkipRand(IVs, pk))
            return false;
        return true;
    }

    private static bool IsMatchEggLocation(PKM pk)
    {
        var expect = pk is PB8 ? Locations.Default8bNone : 0;
        return pk.Egg_Location == expect;
    }

    private bool IsMatchLocation(PKM pk)
    {
        var met = pk.Met_Location;
        if (met == Location)
            return true;
        if ((uint)met > byte.MaxValue)
            return false;
        return Crossover.IsMatchLocation((byte)met);
    }

    private bool IsMatchLevel(PKM pk)
    {
        var met = pk.Met_Level;
        var lvl = Level;
        if (met == lvl)
            return true;
        if (lvl < EncounterArea8.BoostLevel && EncounterArea8.IsBoostedArea60(Location))
            return met == EncounterArea8.BoostLevel;
        return false;
    }

    public EncounterMatchRating GetMatchRating(PKM pk)
    {
        if (pk is { AbilityNumber: 4 } && this.IsPartialMatchHidden(pk.Species, Species))
            return EncounterMatchRating.PartialMatch;

        var req = GetRequirement(pk);
        bool correlation = IsOverworldCorrelationCorrect(pk);
        if ((req == MustHave) != correlation)
            return EncounterMatchRating.DeferredErrors;

        // Only encounter slots can have these marks; defer for collisions.
        if (pk.Species == (int)Core.Species.Shedinja)
        {
            // Loses Mark on evolution to Shedinja, but not affixed ribbon value.
            return pk switch
            {
                IRibbonSetMark8 { RibbonMarkCurry: true } => EncounterMatchRating.DeferredErrors,
                PK8 { AffixedRibbon: (int)RibbonIndex.MarkCurry } => EncounterMatchRating.Deferred,
                _ => EncounterMatchRating.Match,
            };
        }

        if (pk is IRibbonSetMark8 m && (m.RibbonMarkCurry || m.RibbonMarkFishing || !Weather.IsMarkCompatible(m)))
            return EncounterMatchRating.DeferredErrors;

        return EncounterMatchRating.Match;
    }

    #endregion
}