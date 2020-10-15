using System.Collections;
using System.Collections.Generic;
using Entitas;
using UnityEngine;

public static class GameContextExtensions
{
    public static IEnumerable BottomSlotsIndexes(this GameContext context)
    {
        var indexer = context.slotsIndexer.Value;

        for (int i = 0; i < indexer.GetLength(0); i++)
        {
            for (int j = 0; j < indexer.GetLength(1); j++)
            {
                if (indexer[i, j] != null) continue;

                yield return new Vector2Int(i, j);
                break;
            }
        }
    }

    public static Vector3 IndexToPosition(this Vector2Int slot, IGameConfiguration configuration)
    {
        var hIndex = slot.x * 2 + slot.y % 2;

        var position = new Vector3
        (
            (hIndex - configuration.SlotsOffset.x) * configuration.SlotSeparation.x -
            configuration.SlotSeparation.x / 2f,
            -slot.y * configuration.SlotSeparation.y + configuration.SlotsOffset.y,
            0
        );

        return position;
    }

    public static bool IsEmpty(this IEntity[,] slots, int i, int j)
    {
        return i < 0 || i >= slots.GetLength(0)
                     || j < 0 || j >= slots.GetLength(1)
                     || slots[i, j] == null
                     || !slots[i, j].isEnabled
                     || !((GameEntity) slots[i, j]).isBalloon;
    }

    /// <summary>
    /// A slot is unbalanced when it doesn't have two occupied slots above
    /// </summary>
    /// <param name="slots"></param>
    /// <param name="i"></param>
    /// <param name="j"></param>
    /// <returns></returns>
    public static bool IsUnbalanced(this IEntity[,] slots, int i, int j)
    {
        if (j < 0 || i < 0) throw new System.ArgumentException("Invalid argument for index values");

        if (j == 0) return false;

        if (slots.IsEmpty(i, j - 1)) return true;

        var index = i + (j % 2 == 0 ? -1 : 1);

        if (index >= 0 && index < slots.GetLength(0))
        {
            return slots.IsEmpty(index, j - 1);
        }

        return false;
    }

    /// <summary>
    /// This method calculates the weight of a slot entry by taking into account
    /// how many occupied slots are above the source point
    /// </summary>
    /// <param name="slots"></param>
    /// <param name="i"></param>
    /// <param name="j"></param>
    /// <returns></returns>
    public static int CalculateWeight(this IEntity[,] slots, int i, int j)
    {
        if (j == 0)
        {
            return slots.IsEmpty(i, j) ? 0 : 1;
        }

        if (j > 0)
        {
            var weight = slots.IsEmpty(i, j) ? 0 : 1;

            weight += slots.CalculateWeight(i, j - 1);
            weight += slots.CalculateWeight(i + (j % 2 == 0 ? -1 : 1), j - 1);

            return weight;
        }

        return 0;
    }

    /// <summary>
    /// Gets the occupied slots neighboring this position
    /// </summary>
    /// <param name="slots"></param>
    /// <param name="i"></param>
    /// <param name="j"></param>
    /// <returns></returns>
    public static List<IEntity> GetNeighbors(this IEntity[,] slots, int i, int j)
    {
        var neighbors = new List<IEntity>();

        // add left neighbor
        if (!slots.IsEmpty(i - 1, j))
        {
            neighbors.Add(slots[i - 1, j]);
        }

        // add right neighbor
        if (!slots.IsEmpty(i + 1, j))
        {
            neighbors.Add(slots[i + 1, j]);
        }

        // add neighbors above
        if (!slots.IsEmpty(i, j - 1))
        {
            neighbors.Add(slots[i, j - 1]);
        }

        var shift = i + (j % 2 == 0 ? -1 : 1);

        if (!slots.IsEmpty(shift, j - 1))
        {
            neighbors.Add(slots[shift, j - 1]);
        }

        // add neighbors below
        if (!slots.IsEmpty(i, j + 1))
        {
            neighbors.Add(slots[i, j + 1]);
        }

        shift = i + (j % 2 == 0 ? -1 : 1);

        if (!slots.IsEmpty(shift, j + 1))
        {
            neighbors.Add(slots[shift, j + 1]);
        }

        return neighbors;
    }

    /// <summary>
    /// This method finds the closest optimal slot for it to move up
    /// </summary>
    /// <param name="slots"></param>
    /// <param name="i"></param>
    /// <param name="j"></param>
    /// <returns></returns>
    public static Vector2Int? OptimalNextEmptySlot(this IEntity[,] slots, int i, int j)
    {
        if (j <= 0) return null;

        var options = new Vector2Int[]
        {
            new Vector2Int(i, j - 1),
            new Vector2Int(i + (j % 2 == 0 ? -1 : 1), j - 1),
        };

        var weight = 0;
        var optionIndex = -1;

        for (int k = 0; k < options.Length; k++)
        {
            var index = options[k];

            // the index is within the possible values
            if (index.x >= 0 && index.x < slots.GetLength(0) && index.y >= 0 && index.y < slots.GetLength(1))
            {
                // the position is empty so it can be taken
                if (slots.IsEmpty(index.x, index.y))
                {
                    var slotWeight = slots.CalculateWeight(index.x, index.y);

                    if (slotWeight >= weight)
                    {
                        weight = slotWeight;
                        optionIndex = k;
                    }
                }
            }
        }

        if (optionIndex >= 0) return options[optionIndex];

        return null;
    }

    public static void AddScore(this GameContext context, string name, out GameEntity gameLevelProgressEntity)
    {
        var scores = context.GetGroup(GameMatcher.GamePersistentScore);
        var progresses = context.GetGroup(GameMatcher.GameLevelProgress);
        gameLevelProgressEntity = null;

        foreach (var score in scores.GetEntities())
        {
            if (score.gamePersistentScore.Name == name)
            {
                score.ReplaceGamePersistentScore(name, score.gamePersistentScore.Score + 1);
            }
        }

        var level = context.gameLevel.Value;
        var required = GameConfiguration.PointsRequiredForLevel(level + 1);
        var allPass = true;

        foreach (var progress in progresses.GetEntities())
        {
            if (progress.gameLevelProgress.Name == name)
            {
                progress.ReplaceGameLevelProgress(name, progress.gameLevelProgress.Current + 1);
                gameLevelProgressEntity = progress;
            }

            // check if the current progress passes the level requirement
            if (progress.gameLevelProgress.Current < required)
            {
                allPass = false;
            }
        }

        // if all requirements are meet, level up
        if (allPass)
        {
            context.ReplaceGameLevel(level + 1);

            // reset progress
            foreach (var progress in progresses.GetEntities())
            {
                progress.ReplaceGameLevelProgress(progress.gameLevelProgress.Name, 0);
            }

            var e = context.CreateEntity();
            e.isGameEvent = true;
            e.isGameLevelUp = true;
        }
    }
}