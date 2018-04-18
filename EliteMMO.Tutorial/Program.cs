using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using EliteMMO.API;

namespace EliteMMO.Tutorial
{
    public class Program
    {
        /// <summary>
        /// The max size of the entity array.
        /// </summary>
        public const int MaxEntityArraySize = 4096;

        /// <summary>
        /// The max distance for melee attacks.
        /// </summary>
        private const int MeleeDistance = 3;

        public enum ViewMode
        {
            ThirdPerson,
            FirstPerson
        }

        public enum Status : uint
        {
            Fighting = 1,
            Dead1 = 2,
            Dead2 = 3,
        }

        public static void Main(string[] args)
        {
            // Find the POL game instance.
            Process process = FindGameProcess();

            // Exit if no game instance is running.
            EnsureGameStarted(process);

            // Create a new instance of the EliteAPI.
            EliteAPI eliteApi = CreateEliteApi(process);

            while (true)
            {
                // Find all entities.
                IDictionary<int, EliteAPI.EntityEntry> entities = FindEntities(eliteApi);

                // Find the closest mob.
                KeyValuePair<int, EliteAPI.EntityEntry> mob = FindMob(entities);

                // Target the mob.
                TargetMob(mob, eliteApi);

                // Approach the mob.
                ApproachMob(mob, MeleeDistance, eliteApi);

                // Kill the mob.
                EngageMob(mob, MeleeDistance, eliteApi);
            }
        }

        private static void EnsureGameStarted(Process process)
        {
            if (process == null)
            {
                Console.WriteLine("No game process could be found. Press any key to continue.");
                Console.ReadKey();
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// Melee the target until it is killed.
        /// </summary>
        /// <param name="mob"></param>
        /// <param name="meleeDistance"></param>
        /// <param name="eliteApi"></param>
        private static void EngageMob(KeyValuePair<int, EliteAPI.EntityEntry> mob, int meleeDistance, EliteAPI eliteApi)
        {
            if (IsDead(mob)) return;

            while (true)
            {
                // Get updated mob information.
                mob = FindEntity(mob.Key, eliteApi);

                // Stop when the mob is killed.
                if (IsDead(mob)) break;

                // Approach mob if it has run away.
                if (!IsWithinRange(mob, meleeDistance))
                    ApproachMob(mob, meleeDistance, eliteApi);

                // Melee mob until it is dead.
                EngageMob(eliteApi);
            }
        }

        /// <summary>
        /// Makes the player engage the current target.
        /// </summary>
        /// <param name="eliteApi"></param>
        private static void EngageMob(EliteAPI eliteApi)
        {
            if (IsFighting(eliteApi)) return;
            eliteApi.ThirdParty.SendString("/attack on");
            Thread.Sleep(100);
        }

        private static bool IsFighting(EliteAPI eliteApi)
        {
            return eliteApi.Player.Status == (int)Status.Fighting;
        }

        /// <summary>
        /// Determines whether the given entity has been killed.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private static bool IsDead(KeyValuePair<int, EliteAPI.EntityEntry> entity)
        {
            return entity.Value.Status == (uint)Status.Dead1 || entity.Value.Status == (uint)Status.Dead2;
        }

        /// <summary>
        /// Places the cursor on the given target.
        /// </summary>
        /// <param name="mob"></param>
        /// <param name="eliteApi"></param>
        /// <returns></returns>
        private static bool TargetMob(KeyValuePair<int, EliteAPI.EntityEntry> mob, EliteAPI eliteApi)
        {
            return eliteApi.Target.SetTarget(mob.Key);
        }

        private static void ApproachMob(KeyValuePair<int, EliteAPI.EntityEntry> mob, int distanceTolerance, EliteAPI eliteApi)
        {
            if (IsWithinRange(mob, distanceTolerance)) return;

            while (true)
            {
                // Get updated mob information.
                mob = FindEntity(mob.Key, eliteApi);

                // Stop when within melee distance.
                if (IsWithinRange(mob, MeleeDistance)) break;

                // Switch to first person view. 
                SetViewMode(ViewMode.FirstPerson, eliteApi);

                // Get updated player information.
                EliteAPI.XiEntity player = FindPlayer(eliteApi);

                // Make the player look at the target.
                FaceTarget(mob, player, eliteApi);

                // Start moving the player towards the target. 
                StartRunning(eliteApi);

                Thread.Sleep(100);
            }

            StopRunning(eliteApi);
        }

        /// <summary>
        /// Checks if the entity is within range.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="distanceTolerance"></param>
        /// <returns></returns>
        private static bool IsWithinRange(KeyValuePair<int, EliteAPI.EntityEntry> entity, int distanceTolerance)
        {
            return entity.Value.Distance <= distanceTolerance;
        }

        /// <summary>
        /// Makes the player run foward.
        /// </summary>
        /// <param name="eliteApi"></param>
        private static void StartRunning(EliteAPI eliteApi)
        {
            eliteApi.ThirdParty.KeyDown(Keys.NUMPAD8);
        }

        /// <summary>
        /// Stops the player from running anymore. 
        /// </summary>
        /// <param name="eliteApi"></param>
        private static void StopRunning(EliteAPI eliteApi)
        {
            eliteApi.ThirdParty.KeyUp(Keys.NUMPAD8);
        }

        /// <summary>
        /// Find the players information.
        /// </summary>
        /// <param name="eliteApi"></param>
        /// <returns></returns>
        private static EliteAPI.XiEntity FindPlayer(EliteAPI eliteApi)
        {
            return eliteApi.Entity.GetLocalPlayer();
        }

        /// <summary>
        /// Make the player face the given target.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="player"></param>
        /// <param name="eliteApi"></param>
        private static void FaceTarget(KeyValuePair<int, EliteAPI.EntityEntry> target, EliteAPI.XiEntity player, EliteAPI eliteApi)
        {
            byte angle = (byte)(Math.Atan((target.Value.Z - player.Z) / (target.Value.X - player.X)) * -(128.0f / Math.PI));
            if (player.X > target.Value.X) angle += 128;
            double radian = (float)angle / 255 * 2 * Math.PI;
            eliteApi.Entity.SetEntityHPosition(eliteApi.Entity.LocalPlayerIndex, (float)radian);
        }

        /// <summary>
        /// Change the player's view mode.
        /// </summary>
        /// <param name="viewMode"></param>
        /// <param name="eliteApi"></param>
        private static void SetViewMode(ViewMode viewMode, EliteAPI eliteApi)
        {
            eliteApi.Player.ViewMode = (int)viewMode;
        }

        /// <summary>
        /// Get updated information for the entity.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="eliteApi"></param>
        /// <returns></returns>
        private static KeyValuePair<int, EliteAPI.EntityEntry> FindEntity(int index, EliteAPI eliteApi)
        {
            return new KeyValuePair<int, EliteAPI.EntityEntry>(index, eliteApi.Entity.GetStaticEntity(index));
        }

        /// <summary>
        /// Find the mob that's closest to the player.
        /// </summary>
        /// <param name="entities"></param>
        /// <returns></returns>
        private static KeyValuePair<int, EliteAPI.EntityEntry> FindMob(IDictionary<int, EliteAPI.EntityEntry> entities)
        {
            return entities
                .Where(IsMob)
                .OrderBy(Distance)
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets the distance from this entity.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private static float Distance(KeyValuePair<int, EliteAPI.EntityEntry> entity)
        {
            return entity.Value.Distance;
        }

        /// <summary>
        /// Checks whether this entity has the MobFlag set.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private static bool IsMob(KeyValuePair<int, EliteAPI.EntityEntry> entity)
        {
            int mobFlag = 0x10;
            return (entity.Value.SpawnFlags & mobFlag) == 0;
        }

        /// <summary>
        /// Retreives all entities from the EntityArray.
        /// </summary>
        /// <param name="eliteApi"></param>
        /// <remarks>
        /// The entity array is the structure that holds all players, npcs, mobs, and objects.
        /// </remarks>
        private static IDictionary<int, EliteAPI.EntityEntry> FindEntities(EliteAPI eliteApi)
        {
            IDictionary<int, EliteAPI.EntityEntry> entities = new Dictionary<int, EliteAPI.EntityEntry>();

            for (int index = 0; index < MaxEntityArraySize; index++)
            {
                entities.Add(index, eliteApi.Entity.GetStaticEntity(index));
            }

            return entities;
        }

        /// <summary>
        /// Creates a new instance of the EliteAPI for interacting with the game.
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        private static EliteAPI CreateEliteApi(Process process)
        {
            return new EliteAPI(process.Id);
        }

        /// <summary>
        /// Finds the game process for Final Fantasy XI.
        /// </summary>
        /// <returns></returns>
        private static Process FindGameProcess()
        {
            return Process.GetProcessesByName("pol").FirstOrDefault();
        }
    }
}
