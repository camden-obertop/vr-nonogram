using System;
using System.Security.Cryptography;
using UnityEngine;

public class ProgressManager : MonoBehaviour
{
    // Each of these is called after you beat a level. I.e. the earth transition is enabled when you beat the air puzzle
    [Header("Air")] 
    [SerializeField] private GameObject airTransition;
    [SerializeField] private GameObject earthTransition;

    [Header("Earth")] [SerializeField] private GameObject earthLandscape;
    [SerializeField] private GameObject airLandscape;
    [SerializeField] private GameObject wormTransition;

    [Header("Worm")] 
    [SerializeField] private GameObject worms;
    [SerializeField] private GameObject rockTransition;

    [Header("Rocks")]
    [SerializeField] private GameObject rocks;

    public void ActivatePuzzleInWorld(CompletedPuzzle.Puzzle puzzleType)
    {
        switch (puzzleType)
        {
            case CompletedPuzzle.Puzzle.Air:
                airTransition.SetActive(false);
                earthTransition.SetActive(true);
                break;
            case CompletedPuzzle.Puzzle.Earth:
                earthTransition.SetActive(false);
                earthLandscape.SetActive(true);
                airLandscape.SetActive(false);
                wormTransition.SetActive(true);
                break;
            case CompletedPuzzle.Puzzle.Worm:
                wormTransition.SetActive(false);
                worms.SetActive(true);
                rockTransition.SetActive(true);
                break;
            case CompletedPuzzle.Puzzle.Rock:
                rockTransition.SetActive(false);
                rocks.SetActive(true);
                break;
            case CompletedPuzzle.Puzzle.Sun:
                break;
            case CompletedPuzzle.Puzzle.Ocean:
                break;
            case CompletedPuzzle.Puzzle.Clouds:
                break;
            case CompletedPuzzle.Puzzle.River:
                break;
            case CompletedPuzzle.Puzzle.Grass:
                break;
            case CompletedPuzzle.Puzzle.Flower:
                break;
            case CompletedPuzzle.Puzzle.Bee:
                break;
            case CompletedPuzzle.Puzzle.Tree:
                break;
            case CompletedPuzzle.Puzzle.Bush:
                break;
            case CompletedPuzzle.Puzzle.Fruit:
                break;
            case CompletedPuzzle.Puzzle.Fungus:
                break;
            case CompletedPuzzle.Puzzle.Fish:
                break;
            case CompletedPuzzle.Puzzle.Bird:
                break;
            case CompletedPuzzle.Puzzle.Squirrel:
                break;
            case CompletedPuzzle.Puzzle.Fox:
                break;
            case CompletedPuzzle.Puzzle.Human:
                break;
        }
    }
}