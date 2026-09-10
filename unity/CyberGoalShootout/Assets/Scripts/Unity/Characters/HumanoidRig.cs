using UnityEngine;

namespace CyberGoal.Unity.Characters
{
    /// <summary>
    /// The bones of a humanoid, in the order the skinned mesh binds them.
    /// </summary>
    /// <remarks>
    /// Matches §15's minimum rig — root, hips, spine, chest, neck, head, and both
    /// arms, forearms, hands, legs and feet. Fingers are deliberately absent: at
    /// the closest camera the hand is about eleven pixels across, and twenty extra
    /// bones would cost skinning time for detail nobody can resolve. §15 lists
    /// them as optional "where required for close-up quality", which this is not.
    /// </remarks>
    public enum Bone
    {
        Root = 0,
        Hips,
        Spine,
        Chest,
        Neck,
        Head,
        ShoulderL,
        UpperArmL,
        ForearmL,
        HandL,
        ShoulderR,
        UpperArmR,
        ForearmR,
        HandR,
        ThighL,
        ShinL,
        FootL,
        ThighR,
        ShinR,
        FootR,
        Count
    }

    /// <summary>
    /// Where every joint sits on a 1.82 m adult, in metres from the ground.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the numbers everything else is measured against — the cameras,
    /// the keeper's reach, the ball contact. They are close to real human
    /// proportions rather than heroic ones: shoulders at 1.52 give a head that is
    /// roughly one-seventh of standing height, which is what a photograph of an
    /// athlete looks like. Stylised figures push that toward one-sixth and start
    /// to read as cartoon.
    /// </para>
    /// <para>
    /// The first placeholder had a 0.54 m leg whose foot stopped 28 cm above the
    /// turf, and every figure hovered. Getting this right once, here, means a
    /// MakeHuman export dropped in later lands at the same scale.
    /// </para>
    /// </remarks>
    public static class Skeleton
    {
        public const float Height = 1.82f;

        public static readonly Vector3[] Joints = BuildJoints();

        private static Vector3[] BuildJoints()
        {
            var j = new Vector3[(int)Bone.Count];

            j[(int)Bone.Root] = new Vector3(0f, 0f, 0f);
            j[(int)Bone.Hips] = new Vector3(0f, 0.92f, 0f);
            j[(int)Bone.Spine] = new Vector3(0f, 1.13f, 0f);
            j[(int)Bone.Chest] = new Vector3(0f, 1.34f, 0f);
            j[(int)Bone.Neck] = new Vector3(0f, 1.55f, 0f);
            j[(int)Bone.Head] = new Vector3(0f, 1.64f, 0f);

            // Shoulders sit slightly below the neck joint and out at the deltoid,
            // not at the very edge of the silhouette — the arm hangs outside them.
            j[(int)Bone.ShoulderL] = new Vector3(-0.075f, 1.50f, 0f);
            j[(int)Bone.UpperArmL] = new Vector3(-0.185f, 1.47f, 0f);
            j[(int)Bone.ForearmL] = new Vector3(-0.205f, 1.19f, 0f);
            j[(int)Bone.HandL] = new Vector3(-0.215f, 0.94f, 0f);

            j[(int)Bone.ShoulderR] = new Vector3(0.075f, 1.50f, 0f);
            j[(int)Bone.UpperArmR] = new Vector3(0.185f, 1.47f, 0f);
            j[(int)Bone.ForearmR] = new Vector3(0.205f, 1.19f, 0f);
            j[(int)Bone.HandR] = new Vector3(0.215f, 0.94f, 0f);

            j[(int)Bone.ThighL] = new Vector3(-0.095f, 0.92f, 0f);
            j[(int)Bone.ShinL] = new Vector3(-0.100f, 0.50f, 0f);
            j[(int)Bone.FootL] = new Vector3(-0.100f, 0.075f, 0f);

            j[(int)Bone.ThighR] = new Vector3(0.095f, 0.92f, 0f);
            j[(int)Bone.ShinR] = new Vector3(0.100f, 0.50f, 0f);
            j[(int)Bone.FootR] = new Vector3(0.100f, 0.075f, 0f);

            return j;
        }

        /// <summary>Each bone's parent, for building the transform hierarchy.</summary>
        public static readonly Bone[] Parents = BuildParents();

        private static Bone[] BuildParents()
        {
            var p = new Bone[(int)Bone.Count];
            p[(int)Bone.Root] = Bone.Root;
            p[(int)Bone.Hips] = Bone.Root;
            p[(int)Bone.Spine] = Bone.Hips;
            p[(int)Bone.Chest] = Bone.Spine;
            p[(int)Bone.Neck] = Bone.Chest;
            p[(int)Bone.Head] = Bone.Neck;

            p[(int)Bone.ShoulderL] = Bone.Chest;
            p[(int)Bone.UpperArmL] = Bone.ShoulderL;
            p[(int)Bone.ForearmL] = Bone.UpperArmL;
            p[(int)Bone.HandL] = Bone.ForearmL;

            p[(int)Bone.ShoulderR] = Bone.Chest;
            p[(int)Bone.UpperArmR] = Bone.ShoulderR;
            p[(int)Bone.ForearmR] = Bone.UpperArmR;
            p[(int)Bone.HandR] = Bone.ForearmR;

            p[(int)Bone.ThighL] = Bone.Hips;
            p[(int)Bone.ShinL] = Bone.ThighL;
            p[(int)Bone.FootL] = Bone.ShinL;

            p[(int)Bone.ThighR] = Bone.Hips;
            p[(int)Bone.ShinR] = Bone.ThighR;
            p[(int)Bone.FootR] = Bone.ShinR;

            return p;
        }

        public static string NameOf(Bone bone) => bone.ToString();
    }
}
