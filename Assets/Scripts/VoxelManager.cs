using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

public struct Clue
{
    public Clue(bool blank, int voxelCount, int gapCount)
    {
        Blank = blank;
        VoxelCount = voxelCount;
        GapCount = gapCount;
    }

    public bool Blank;
    public int VoxelCount;
    public int GapCount;
}

public class VoxelManager : MonoBehaviour
{
    [SerializeField] private int length, height, width; // length = x, height = y, width = z 
    [SerializeField] private float rotateSpeed;

    [SerializeField] private GameObject mainCamera;
    [SerializeField] private GameObject cube;

    private int _visibleLayersX, _visibleLayersY, _visibleLayersZ;
    private GameObject[,,] _voxels;
    private Clue[,] _frontClues, _sideClues, _topClues;
    private bool[,,] _solution;
    private Vector3 _target = Vector3.zero;
    private Transform _cameraTransform;
    private bool _coroutineFinished = true;
    private bool _canVerticallyRotate = true;

    private void Start()
    {
        _visibleLayersX = length - 1;
        _visibleLayersY = height - 1;
        _visibleLayersZ = width - 1;

        CreateSolution();
        InitializeVoxels();

        _cameraTransform = mainCamera.transform;

        _frontClues = new Clue[length, height];
        _sideClues = new Clue[width, height];
        _topClues = new Clue[length, width];

        NumberAllVoxels();
    }

    private void InitializeVoxels()
    {
        _voxels = new GameObject[length, height, width];
        for (int i = 0; i < length; i++)
        {
            for (int j = 0; j < height; j++)
            {
                for (int k = 0; k < width; k++)
                {
                    _voxels[i, j, k] = Instantiate(cube, new Vector3(i - length / 2, j - height / 2, k - width / 2), Quaternion.identity, transform);
                    _voxels[i, j, k].GetComponent<Voxel>().IsPuzzleVoxel = _solution[i, j, k];
                }
            }
        }
    }

    private void Update()
    {
        ManageRotations();
        ManageVisibleLayers();
    }

    private void CreateSolution()
    {
        _solution = new bool[length, height, width];

        for (int i = 0; i < length; i++)
        {
            for (int j = 0; j < height; j++)
            {
                for (int k = 0; k < width; k++)
                {
                    _solution[i, j, k] = Convert.ToBoolean(Random.Range(0, 2));
                }
            }
        }

        PrintSolution();
    }

    private void PrintSolution()
    {
        string matrices = "Solution:";
        for (int k = 0; k < width; k++)
        {
            matrices += $"\n Layer {k + 1}: \n";
            for (int j = 0; j < height; j++)
            {
                matrices += "| ";
                for (int i = 0; i < length; i++)
                {
                    matrices += $"{(_solution[i, j, k] ? 1 : 0)} ";
                }
                matrices += "|\n";
            }
        }

        Debug.Log(matrices);
    }

    private void NumberAllVoxels()
    {
        // Populate front clues
        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < length; i++)
            {
                int voxelCount = 0;
                int gapCount = 0;
                bool previousWasGap = false;

                for (int k = 0; k < width; k++)
                {
                    if (_solution[i, j, k])
                    {
                        voxelCount++;
                        previousWasGap = false;
                    }
                    else if (k != width - 1 && voxelCount > 0 && !previousWasGap)
                    {
                        gapCount++;
                        previousWasGap = true;
                    }

                    if (k == width - 1 && voxelCount <= 1)
                    {
                        gapCount = 0;
                    }
                }

                _frontClues[i, j] = new Clue(blank: false, voxelCount: voxelCount, gapCount: gapCount);
            }
        }

        // Populate side clues
        for (int j = 0; j < height; j++)
        {
            for (int k = 0; k < width; k++)
            {
                int voxelCount = 0;
                int gapCount = 0;
                bool previousWasGap = false;

                for (int i = 0; i < length; i++)
                {
                    if (_solution[i, j, k])
                    {
                        voxelCount++;
                        previousWasGap = false;
                    }
                    else if (i != length - 1 && voxelCount > 0 && !previousWasGap)
                    {
                        gapCount++;
                        previousWasGap = true;
                    }

                    if (i == length - 1 && voxelCount <= 1)
                    {
                        gapCount = 0;
                    }
                }

                _sideClues[k, j] = new Clue(blank: false, voxelCount: voxelCount, gapCount: gapCount);
            }
        }

        // populate top clues
        for (int k = 0; k < width; k++)
        {
            for (int i = 0; i < length; i++)
            {
                int voxelCount = 0;
                int gapCount = 0;
                bool previousWasGap = false;
                bool gap = false;

                for (int j = 0; j < height; j++)
                {
                    if (_solution[i, j, k])
                    {
                        voxelCount++;
                        if (previousWasGap)
                        {
                            gap = true;
                        }
                        previousWasGap = false;
                    }
                    else if (j != height - 1 && voxelCount > 0 && !previousWasGap)
                    {
                        gapCount++;
                        previousWasGap = true;
                    }

                    if (j == height - 1 && !gap)
                    {
                        gapCount = 0;
                    }
                }

                _topClues[i, k] = new Clue(blank: false, voxelCount: voxelCount, gapCount: gapCount);
            }
        }

        string frontcluesstring = "front clues:\n";
        for (int j = 0; j < height; j++)
        {
            frontcluesstring += "| ";
            for (int i = 0; i < length; i++)
            {
                frontcluesstring += $"({_frontClues[i, j].VoxelCount}^{_frontClues[i, j].GapCount}) ";
            }
            frontcluesstring += "|\n";
        }
        Debug.Log(frontcluesstring);

        string sidecluesstring = "side clues:\n";
        for (int j = 0; j < height; j++)
        {
            sidecluesstring += "| ";
            for (int k = 0; k < width; k++)
            {
                sidecluesstring += $"({_sideClues[k, j].VoxelCount}^{_sideClues[k, j].GapCount}) ";
            }
            sidecluesstring += "|\n";
        }
        Debug.Log(sidecluesstring);

        string topcluesstring = "top clues:\n";
        for (int k = width - 1; k >= 0; k--)
        {
            topcluesstring += "| ";
            for (int i = 0; i < length; i++)
            {
                topcluesstring += $"({_topClues[i, k].VoxelCount}^{_topClues[i, k].GapCount}) ";
            }
            topcluesstring += "|\n";
        }
        Debug.Log(topcluesstring);
    }

    private void ManageVisibleLayers()
    {
        if (_coroutineFinished)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) && _visibleLayersX > 0)
            {
                for (int i = 0; i < _visibleLayersY + 1; i++)
                {
                    for (int j = 0; j < _visibleLayersZ + 1; j++)
                    {
                        ChangeVoxelVisible(_voxels[_visibleLayersX, i, j], false);
                        // voxels[visibleLayersX, i, j].SetActive(false);
                    }
                }
                _visibleLayersX--;
            }
            if (Input.GetKeyDown(KeyCode.Alpha2) && _visibleLayersX < length - 1)
            {
                _visibleLayersX++;
                for (int i = 0; i < _visibleLayersY + 1; i++)
                {
                    for (int j = 0; j < _visibleLayersZ + 1; j++)
                    {
                        ChangeVoxelVisible(_voxels[_visibleLayersX, i, j], true);
                        // voxels[visibleLayersX, i, j].SetActive(true);
                    }
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha3) && _visibleLayersY > 0)
            {
                for (int i = 0; i < _visibleLayersX + 1; i++)
                {
                    for (int j = 0; j < _visibleLayersZ + 1; j++)
                    {
                        ChangeVoxelVisible(_voxels[i, _visibleLayersY, j], false);
                        // voxels[i, visibleLayersY, j].SetActive(false);
                    }
                }
                _visibleLayersY--;
            }
            if (Input.GetKeyDown(KeyCode.Alpha4) && _visibleLayersY < height - 1)
            {
                _visibleLayersY++;
                for (int i = 0; i < _visibleLayersX + 1; i++)
                {
                    for (int j = 0; j < _visibleLayersZ + 1; j++)
                    {
                        ChangeVoxelVisible(_voxels[i, _visibleLayersY, j], true);
                        // voxels[i, visibleLayersY, j].SetActive(true);
                    }
                }
            }
            if (Input.GetKeyDown(KeyCode.Alpha5) && _visibleLayersZ > 0)
            {
                for (int i = 0; i < _visibleLayersX + 1; i++)
                {
                    for (int j = 0; j < _visibleLayersY + 1; j++)
                    {
                        ChangeVoxelVisible(_voxels[i, j, _visibleLayersZ], false);
                        // voxels[i, j, visibleLayersZ].SetActive(false);
                    }
                }
                _visibleLayersZ--;
            }
            if (Input.GetKeyDown(KeyCode.Alpha6) && _visibleLayersZ < width - 1)
            {
                _visibleLayersZ++;
                for (int i = 0; i < _visibleLayersX + 1; i++)
                {
                    for (int j = 0; j < _visibleLayersY + 1; j++)
                    {
                        ChangeVoxelVisible(_voxels[i, j, _visibleLayersZ], true);
                        // voxels[i, j, visibleLayersZ].SetActive(true);
                    }
                }
            }
        }
    }

    private void ChangeVoxelVisible(GameObject voxel, bool isActivated)
    {
        if (isActivated)
        {
            _coroutineFinished = false;
            StartCoroutine(GrowVoxel(voxel));
        }
        else
        {
            _coroutineFinished = false;
            StartCoroutine(ShrinkVoxel(voxel));
        }
    }

    IEnumerator GrowVoxel(GameObject voxel)
    {
        bool grow = true;
        while (grow)
        {
            Vector3 normalSize = Vector3.one;
            voxel.SetActive(true);
            if (voxel.transform.localScale != normalSize)
            {
                voxel.transform.localScale += new Vector3(0.1f, 0.1f, 0.1f);
            }
            else
            {
                voxel.transform.localScale = normalSize;
                grow = false;
            }
            yield return new WaitForSeconds(0.02f);
        }
        _coroutineFinished = true;
    }

    private IEnumerator ShrinkVoxel(GameObject voxel)
    {
        _coroutineFinished = false;
        bool shrink = true;
        while (shrink)
        {
            Vector3 smallSize = new Vector3(0.1f, 0.1f, 0.1f);
            if (voxel.transform.localScale != smallSize)
            {
                voxel.transform.localScale -= new Vector3(0.1f, 0.1f, 0.1f);
            }
            else
            {
                voxel.transform.localScale = smallSize;
                voxel.SetActive(false);
                shrink = false;
            }
            yield return new WaitForSeconds(0.02f);
        }
        _coroutineFinished = true;
    }

    private void ManageRotations()
    {
        float timeSpeed = rotateSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.A))
        {
            transform.RotateAround(_target, transform.up, timeSpeed);
        }

        if (Input.GetKey(KeyCode.D))
        {
            transform.RotateAround(_target, transform.up, -timeSpeed);
        }

        if (Input.GetKey(KeyCode.W) && _canVerticallyRotate)
        {
            transform.RotateAround(_target, _cameraTransform.right, timeSpeed);
        }

        if (Input.GetKey(KeyCode.S) && _canVerticallyRotate)
        {
            transform.RotateAround(_target, _cameraTransform.right, -timeSpeed);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            transform.rotation = Quaternion.identity;
        }
    }
}
