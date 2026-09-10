using System.Collections.Generic;
using System.Linq;

namespace CyberGoal.Core.Teams
{
    /// <summary>A colour as plain bytes, so Core stays free of UnityEngine.Color.</summary>
    public readonly struct Rgb
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;

        public Rgb(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public static Rgb FromHex(int hex)
            => new Rgb((byte)((hex >> 16) & 0xFF), (byte)((hex >> 8) & 0xFF), (byte)(hex & 0xFF));

        public int ToHex() => (R << 16) | (G << 8) | B;
    }

    public enum League
    {
        NeonEurope,
        AsiaFuture,
        AmericaNexus
    }

    /// <summary>
    /// One team.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>There are deliberately no competitive attributes here.</b> §36 rules out
    /// pay-to-win, and the surest way to honour that is to leave no field a
    /// balance change could ever be smuggled into. A team is a name, a league and
    /// three colours. If one team could shoot harder than another, picking a team
    /// would stop being self-expression and start being a decision with a correct
    /// answer.
    /// </para>
    /// <para>
    /// §34's player attributes — power, accuracy, curve, composure, reflex, dive —
    /// belong on the <em>player</em>, not the team, and are not part of Phase A/B.
    /// When they arrive they must be per-character and cosmetically unlockable
    /// only. A test in this suite asserts the shape of this record so that the
    /// rule survives someone adding "just a small" strength rating later.
    /// </para>
    /// <para>
    /// Every name is invented. §21 and §32 both forbid real clubs, and the brief
    /// is explicit that no existing team, badge or branding may appear.
    /// </para>
    /// </remarks>
    public sealed class Team
    {
        public string Id { get; }
        public string Name { get; }
        public string ShortName { get; }
        public League League { get; }
        public Rgb Primary { get; }
        public Rgb Secondary { get; }
        /// <summary>The neon accent, used for trim and the goal surge.</summary>
        public Rgb Neon { get; }

        public Team(string id, string name, string shortName, League league,
            int primary, int secondary, int neon)
        {
            Id = id;
            Name = name;
            ShortName = shortName;
            League = league;
            Primary = Rgb.FromHex(primary);
            Secondary = Rgb.FromHex(secondary);
            Neon = Rgb.FromHex(neon);
        }
    }

    /// <summary>
    /// The teams, per §32.
    /// </summary>
    /// <remarks>
    /// A static list for now because Phase A/B has no save file to read one from.
    /// The shape is what matters: <see cref="All"/> is the only way anything gets
    /// a team, so swapping this for a ScriptableObject or a downloaded manifest
    /// later changes this file and nothing else.
    /// </remarks>
    public static class TeamCatalogue
    {
        public static readonly IReadOnlyList<Team> All = new List<Team>
        {
            // NEON EUROPE
            new Team("lnv", "London Voltage", "LNV", League.NeonEurope, 0x1B2A4A, 0xE8ECF5, 0x38D6FF),
            new Team("bcr", "Berlin Circuit", "BCR", League.NeonEurope, 0x171A1F, 0xC9CDD4, 0xFF4D3D),
            new Team("mnv", "Madrid Nova", "MNV", League.NeonEurope, 0xF2F4F8, 0x2A2F3A, 0xFFC93D),
            new Team("ppl", "Paris Pulse", "PPL", League.NeonEurope, 0x101A3C, 0xDDE3F0, 0xE0389A),
            new Team("rti", "Rome Titans", "RTI", League.NeonEurope, 0x6B1220, 0xF0D9A8, 0xFF7A2F),

            // ASIA FUTURE
            new Team("tro", "Tokyo Ronin", "TRO", League.AsiaFuture, 0x1A1520, 0xE9E2D8, 0xC0392B),
            new Team("spl", "Seoul Pulse", "SPL", League.AsiaFuture, 0x0E2740, 0xDCE9F2, 0x2AF5C8),
            new Team("dgd", "Delhi Guardians", "DGD", League.AsiaFuture, 0x123A2B, 0xF4EBD0, 0xFFB020),
            new Team("shd", "Shanghai Dragons", "SHD", League.AsiaFuture, 0x2B0F1C, 0xF0DCE4, 0xFF2E63),
            new Team("sgo", "Singapore Orbit", "SGO", League.AsiaFuture, 0x0B2233, 0xD9EEF7, 0x4DE1FF),

            // AMERICA NEXUS
            new Team("nyn", "New York Neon", "NYN", League.AmericaNexus, 0x14161C, 0xE4E7EC, 0x7DF53A),
            new Team("laq", "Los Angeles Quantum", "LAQ", League.AmericaNexus, 0x2A1246, 0xEFE6FA, 0xB14DFF),
            new Team("rbl", "Rio Blaze", "RBL", League.AmericaNexus, 0x0F3B2E, 0xFBF3D0, 0xFFD23F),
            new Team("mcs", "Mexico City Storm", "MCS", League.AmericaNexus, 0x1C2B33, 0xE6EDF0, 0x00E5A0),
        };

        public static Team ById(string id) => All.FirstOrDefault(t => t.Id == id);

        public static IEnumerable<Team> InLeague(League league) => All.Where(t => t.League == league);

        /// <summary>The two sides used before a team-select screen exists.</summary>
        public static Team DefaultHome => ById("lnv");

        public static Team DefaultAway => ById("tro");
    }
}
