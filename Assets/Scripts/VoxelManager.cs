using System;
using System.Collections;
using CsharpVoxReader;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Valve.VR;

public struct Clue
{
    public Clue(bool blank, int voxelCount, int gapCount)
    {
        Blank = blank;
        VoxelCount = voxelCount;
        GapCount = gapCount;
        Complete = false;
    }

    public bool Blank;
    public int VoxelCount;
    public int GapCount;
    public bool Complete;
}

[Serializable]
public struct VoxelInfo
{
    public VoxelInfo(VoxelManager.VoxelState voxelState, Color voxelColor, Vector3Int position)
    {
        VoxelState = voxelState;
        VoxelColor = voxelColor;
        Position = position;
    }

    public VoxelManager.VoxelState VoxelState;
    public Color VoxelColor;
    public Vector3Int Position;
}

public class VoxelManager : MonoBehaviour
{
    public enum GameMode
    {
        Build,
        Destroy,
        Mark,
    }

    public enum VoxelState
    {
        Unmarked = 0,
        Marked = 1,
        Cleared = 2,
    }

    [SerializeField] private Puzzle puzzleObject;

    [Header("Random Generation")] 
    [SerializeField] private bool generateRandomPuzzle;
    [SerializeField] private int length, height, width; // length = x, height = y, width = z 
    
    [Header("Settings")]
    [SerializeField] private float rotateSpeed;

    [Header("Dependencies")]
    [SerializeField] private GameObject cube;
    [SerializeField] private GameObject completedPuzzle;
    [SerializeField] private Material _clearMaterial;
    [SerializeField] private GameObject completedParticles;

    [Header("Sounds")]
    [SerializeField] private GameObject _hideLayerSound;
    [SerializeField] private GameObject _showLayerSound;
    [SerializeField] private GameObject _switchModeSound;

    private VoxelState[,,] _voxelStates;
    public VoxelState[,,] VoxelStates
    {
        get => _voxelStates;
        set => _voxelStates = value;
    }

    private GameObject _mainCamera;
    private bool _canEditPuzzle = true;
    public bool CanEditPuzzle => _canEditPuzzle;

    private int _visibleLayersX, _visibleLayersY, _visibleLayersZ;
    private Stack<GameObject> _hiddenVoxels;
    private bool _faceVisibilityChanged;
    private int _currentLayerPosX, _currentLayerNegX, _currentLayerPosY, _currentLayerNegY, _currentLayerPosZ, _currentLayerNegZ;

    private GameObject[,,] _voxels;
    private Clue[,] _frontClues, _sideClues, _topClues;
    private VoxelState[,,] _solution;
    private Vector3 _target = Vector3.zero;
    private Transform _cameraTransform;
    private bool _coroutineFinished = true;
    private bool _canVerticallyRotate = true;
    private VoxelInfo[,,] _puzzle;
    private Dictionary<string, Vector3> _cubeFaceCenterCoords;
    private string _nearestFace;
    private Coroutine _checkSolutionCoroutine;
    private GameObject _picrossPlayer;
    private GameObject _overworldPlayer;

    private bool modeSwitchable = true;

    private GameMode _currentGameMode = GameMode.Mark;

    [SerializeField] private Material _unselectedModeSelectorMat;
    [SerializeField] private Material _selectedModeSelectorMat;

    public GameMode CurrentGameMode
    {
        get => _currentGameMode;
        set => _currentGameMode = value;
    }

    public void BeginPuzzle(Puzzle newPuzzleObject, GameObject picrossPlayer, GameObject overworldPlayer)
    {
        _mainCamera = Camera.main.gameObject;
        _picrossPlayer = picrossPlayer;
        _overworldPlayer = overworldPlayer;
        puzzleObject = newPuzzleObject;

        _target = transform.position;
        
        _visibleLayersX = length - 1;
        _visibleLayersY = height - 1;
        _visibleLayersZ = width - 1;

        _hiddenVoxels = new Stack<GameObject>();
        _faceVisibilityChanged = false;

        InitializeCurrentLayers();

        if (generateRandomPuzzle || puzzleObject == null)
        {
            CreateSolution();
        }
        else
        {
            _puzzle = SetPuzzleVoxels(
                puzzleObject.Palette, 
                puzzleObject.Data, 
                puzzleObject.SizeX, 
                puzzleObject.SizeY, 
                puzzleObject.SizeZ);
            _solution = GetSolutionVoxelStates(_puzzle);
            length = _solution.GetLength(0);
            height = _solution.GetLength(1);
            width = _solution.GetLength(2);
        }
        InitializeVoxels();

        InitializeGameMode();

        _cameraTransform = _mainCamera.transform;

        _frontClues = new Clue[length, height];
        _sideClues = new Clue[width, height];
        _topClues = new Clue[length, width];

        NumberAllVoxels();

        // VoxelState[,,] calculatedSolution = Validator.IsValid(_solution, _frontClues, _sideClues, _topClues);
        //
        // Debug.Log("Calculated Sol:");
        // PrintSolution(calculatedSolution);

        _cubeFaceCenterCoords = new Dictionary<string, Vector3>();
        _nearestFace = "";
        InitializeCubeFaceCenterCoords();
    }

    private void InitializeGameMode()
    {
        GameObject.FindGameObjectWithTag("Mark").GetComponent<ChangeModeSelector>().selected = true;
        GameObject.FindGameObjectWithTag("Build").GetComponent<ChangeModeSelector>().selected = false;
        GameObject.FindGameObjectWithTag("Destroy").GetComponent<ChangeModeSelector>().selected = false;
        GameObject.FindGameObjectWithTag("Mark").GetComponent<MeshRenderer>().material = _selectedModeSelectorMat;
        GameObject.FindGameObjectWithTag("Build").GetComponent<MeshRenderer>().material = _unselectedModeSelectorMat;
        GameObject.FindGameObjectWithTag("Destroy").GetComponent<MeshRenderer>().material = _unselectedModeSelectorMat;
        _currentGameMode = GameMode.Mark;
        MakeMarkable();
        modeSwitchable = true;
        Debug.Log(_currentGameMode);
    }

    public void UpdateAdjacentVoxelHints(Vector3 indexPosition)
    {
        int i = (int) indexPosition.x;
        int j = (int) indexPosition.y;
        int k = (int) indexPosition.z;

        if (!_frontClues[i, j].Blank) // if the clues in the line aren't blank
        {
            // Update voxel in front
            if (k > 0) // if the voxel is not a boundary
            {
                Voxel voxelToUpdate = _voxels[i, j, k - 1].GetComponent<Voxel>();

                if (voxelToUpdate.IsVisible) // if the voxel is visible to the player, then update its hint
                {
                    voxelToUpdate.Hints[(int) VoxelSide.Rear].SetHintText(
                        _frontClues[i, j].VoxelCount,
                        _frontClues[i, j].GapCount);
                }
                else
                {
                    voxelToUpdate.Hints[(int) VoxelSide.Rear].ClearHintText();
                }
            }

            // Update voxel in rear
            if (k < width - 1) // if the voxel is not a boundary
            {
                Voxel voxelToUpdate = _voxels[i, j, k + 1].GetComponent<Voxel>();

                if (voxelToUpdate.IsVisible) // if the voxel is visible to the player, then update its hint
                {
                    voxelToUpdate.Hints[(int) VoxelSide.Front].SetHintText(
                        _frontClues[i, j].VoxelCount,
                        _frontClues[i, j].GapCount);
                }
                else
                {
                    voxelToUpdate.Hints[(int) VoxelSide.Front].ClearHintText();
                }
            }
        }
        
        if (!_sideClues[k, j].Blank) // if the clues in the line aren't blank
        {
            // Update voxel to the left
            if (i > 0) // if the voxel is not a boundary
            {
                Voxel voxelToUpdate = _voxels[i - 1, j, k].GetComponent<Voxel>();

                if (voxelToUpdate.IsVisible) // if the voxel is visible to the player, then update its hint
                {
                    voxelToUpdate.Hints[(int) VoxelSide.RightSide].SetHintText(
                        _sideClues[k, j].VoxelCount,
                        _sideClues[k, j].GapCount);
                }
                else
                {
                    voxelToUpdate.Hints[(int) VoxelSide.RightSide].ClearHintText();
                }
            }
            
            // Update voxel to the right
            if (i < length - 1) // if the voxel is not a boundary
            {
                Voxel voxelToUpdate = _voxels[i + 1, j, k].GetComponent<Voxel>();

                if (voxelToUpdate.IsVisible) // if the voxel is visible to the player, then update its hint
                {
                    voxelToUpdate.Hints[(int) VoxelSide.LeftSide].SetHintText(
                        _sideClues[k, j].VoxelCount,
                        _sideClues[k, j].GapCount);
                }
                else
                {
                    voxelToUpdate.Hints[(int) VoxelSide.LeftSide].ClearHintText();
                }
            }
        }

        if (!_topClues[i, k].Blank) // if the clues in the line aren't blank
        {
            // Update voxel below
            if (j > 0) // if the voxel is not a boundary
            {
                Voxel voxelToUpdate = _voxels[i, j - 1, k].GetComponent<Voxel>();

                if (voxelToUpdate.IsVisible) // if the voxel is visible to the player, then update its hint
                {
                    voxelToUpdate.Hints[(int) VoxelSide.Top].SetHintText(
                        _topClues[i, k].VoxelCount,
                        _topClues[i, k].GapCount);
                }
                else
                {
                    voxelToUpdate.Hints[(int) VoxelSide.Top].ClearHintText();
                }
            }
            
            // Update voxel above
            if (j < height - 1) // if the voxel is not a boundary
            {
                Voxel voxelToUpdate = _voxels[i, j + 1, k].GetComponent<Voxel>();

                if (voxelToUpdate.IsVisible) // if the voxel is visible to the player, then update its hint
                {
                    voxelToUpdate.Hints[(int) VoxelSide.Bottom].SetHintText(
                        _topClues[i, k].VoxelCount,
                        _topClues[i, k].GapCount);
                }
                else
                {
                    voxelToUpdate.Hints[(int) VoxelSide.Bottom].ClearHintText();
                }
            }
        }
    }

    public void UpdateVoxelState(Vector3Int position, VoxelState state)
    {
        _voxelStates[position.x, position.y, position.z] = state;

        if (state != VoxelState.Unmarked)
        {
            _checkSolutionCoroutine = StartCoroutine(IsCurrentStateCorrect(_voxelStates, _solution));
        }
    }

    private IEnumerator IsCurrentStateCorrect(VoxelManager.VoxelState[,,] voxelStates, VoxelManager.VoxelState[,,] solution)
    {
        bool correct = true;

        for (int i = 0; i < voxelStates.GetLength(0); i++)
        {
            for (int j = 0; j < voxelStates.GetLength(1); j++)
            {
                for (int k = 0; k < voxelStates.GetLength(2); k++)
                {
                    if (voxelStates[i, j, k] != solution[i, j, k])
                        correct = false;
                }
            }
        }

        if (correct)
        {
            _canEditPuzzle = false;

            Instantiate(completedParticles, transform.position, Quaternion.identity);
            ResetAllLayers();
            if (puzzleObject.PuzzleType == CompletedPuzzle.Puzzle.Air)
            {
                GameObject.FindGameObjectWithTag("VO").GetComponent<VOManager>().CompleteFirstPuzzle();
            }
            else if (puzzleObject.PuzzleType == CompletedPuzzle.Puzzle.Earth)
            {
                GameObject.FindGameObjectWithTag("VO").GetComponent<VOManager>().FinishEarthPuzzle();
            }

            for (int i = 0; i < voxelStates.GetLength(0); i++)
            {
                for (int j = 0; j < voxelStates.GetLength(1); j++)
                {
                    for (int k = 0; k < voxelStates.GetLength(2); k++)
                    {
                        _voxels[i, j, k].GetComponent<Voxel>().ClearedPuzzle(_puzzle[i, j, k].VoxelColor);
                    }
                }
            }

            yield return new WaitForSeconds(4f);

            while (transform.localScale.x > 0.1f)
            {
                transform.localScale -= Vector3.one * 0.02f;
                yield return new WaitForSeconds(0.01f);
            }

            GameObject completedPuzzleInstance = Instantiate(completedPuzzle);
            completedPuzzleInstance.GetComponent<CompletedPuzzle>().PuzzleType = puzzleObject.PuzzleType;
            _overworldPlayer.transform.position = _picrossPlayer.transform.position;
            _overworldPlayer.transform.rotation = _picrossPlayer.transform.rotation;
            _picrossPlayer.SetActive(false);
            yield return new WaitForEndOfFrame();
            GameObject.FindGameObjectWithTag("VFX").GetComponent<VFXManager>().SwitchToOverworldMode();
            _overworldPlayer.SetActive(true);
            if (puzzleObject.PuzzleType == CompletedPuzzle.Puzzle.Human)
            {
                GameObject.FindGameObjectWithTag("VO").GetComponent<VOManager>().EndingDriver();
            }
            Destroy(gameObject);
        }

        yield return correct;
    }

    private void InitializeVoxels()
    {
        _voxels = new GameObject[length, height, width];
        _voxelStates = new VoxelState[length, height, width];

        for (int i = 0; i < length; i++)
        {
            for (int j = 0; j < height; j++)
            {
                for (int k = 0; k < width; k++)
                {
                    _voxels[i, j, k] = Instantiate(cube, transform.position + new Vector3((i - length/2) * cube.transform.localScale.x, (j - height/2) * cube.transform.localScale.y, (k - width/2) * cube.transform.localScale.z),
                        Quaternion.identity, transform);
                    _voxels[i, j, k].GetComponent<Voxel>().IsPuzzleVoxel = _solution[i, j, k] == VoxelState.Marked;
                    _voxels[i, j, k].GetComponent<Voxel>().Manager = this;
                    _voxels[i, j, k].GetComponent<Voxel>().IndexPosition = new Vector3Int(i, j, k);

                    _voxelStates[i, j, k] = VoxelState.Unmarked;
                }
            }
        }
    }

    private void InitializeCubeFaceCenterCoords()
    {
        _cubeFaceCenterCoords.Add("positiveX", Vector3.zero);
        _cubeFaceCenterCoords.Add("negativeX", Vector3.zero);
        _cubeFaceCenterCoords.Add("positiveY", Vector3.zero);
        _cubeFaceCenterCoords.Add("negativeY", Vector3.zero);
        _cubeFaceCenterCoords.Add("positiveZ", Vector3.zero);
        _cubeFaceCenterCoords.Add("negativeZ", Vector3.zero);
    }

    private void Update()
    {
        ManageRotations();
        ManageVisibleLayers();
        ManageMode();
        GetNearestFace();
        ManageNearestLayer();
    }

    private void InitializeCurrentLayers() {
        _currentLayerPosX = length - 1;
        _currentLayerNegX = 0;
        _currentLayerPosY = height - 1;
        _currentLayerNegY = 0;
        _currentLayerPosZ = width - 1;
        _currentLayerNegZ = 0;
    }

    private bool CheckIfFaceChanged(string currentFace) {
        bool faceChanged = false;

        bool posXmod = _currentLayerPosX != length - 1;
        bool negXmod = _currentLayerNegX != 0;
        bool posYmod = _currentLayerPosY != height - 1;
        bool negYmod = _currentLayerNegY != 0;
        bool posZmod = _currentLayerPosZ != width - 1;
        bool negZmod = _currentLayerNegZ != 0;

        if (currentFace == "posX" && (negXmod || posYmod || negYmod || posZmod || negZmod)) {
            faceChanged = true;
        } else if (currentFace == "negX" && (posXmod || posYmod || negYmod || posZmod || negZmod)) {
            faceChanged = true;
        } else if (currentFace == "posY" && (posXmod || negXmod || negYmod || posZmod || negZmod)) {
            faceChanged = true;
        } else if (currentFace == "negY" && (posXmod || negXmod || posYmod || posZmod || negZmod)) {
            faceChanged = true;
        } else if (currentFace == "posZ" && (posXmod || negXmod || posYmod || negYmod || negZmod)) {
            faceChanged = true;
        } else if (currentFace == "negZ" && (posXmod || negXmod || posYmod || negYmod || posZmod)) {
            faceChanged = true;
        }

        return faceChanged;
    }

    private void ResetAllLayers() {
        Instantiate(_showLayerSound);
        InitializeCurrentLayers();
        while (_hiddenVoxels.Count > 0) {
            GameObject tempVoxel = _hiddenVoxels.Pop();
            ChangeVoxelVisible(tempVoxel, true);
        }
    }

    private void ManageNearestLayer() {
        bool vrHideLayer = SteamVR_Actions.picross.HideLayer[SteamVR_Input_Sources.Any].stateDown;
        bool vrShowLayer = SteamVR_Actions.picross.ShowLayer[SteamVR_Input_Sources.Any].stateDown;
        if (_coroutineFinished) {
            switch (_nearestFace) {
                case "positiveX":
                    if ((vrHideLayer || Input.GetKeyDown(KeyCode.Alpha9)) && _currentLayerPosX > 0) {
                        if (CheckIfFaceChanged("posX")) {
                            ResetAllLayers();
                        } else {
                            Instantiate(_hideLayerSound);
                            Debug.Log("Hide positiveX");
                            for (int i = 0; i < height; i++) {
                                for (int j = 0; j < width; j++) {
                                    _hiddenVoxels.Push(_voxels[_currentLayerPosX, i, j]);
                                    ChangeVoxelVisible(_voxels[_currentLayerPosX, i, j], false);
                                }
                            }
                            _currentLayerPosX--;
                        }
                    } else if ((vrShowLayer || Input.GetKeyDown(KeyCode.Alpha0)) && _hiddenVoxels.Count > 0) {
                        if (CheckIfFaceChanged("posX")) {
                            ResetAllLayers();
                        } else {
                            Instantiate(_showLayerSound);
                            Debug.Log("Show positiveX");
                            for (int i = 0; i < height * width; i++) {
                                GameObject tempVoxel = _hiddenVoxels.Pop();
                                ChangeVoxelVisible(tempVoxel, true);
                            }
                            _currentLayerPosX++;
                        }
                    }
                    break;
                case "negativeX":
                    if ((vrHideLayer || Input.GetKeyDown(KeyCode.Alpha9)) && _currentLayerNegX < length - 1) {
                        if (CheckIfFaceChanged("negX")) {
                            ResetAllLayers();
                        } else {
                            Instantiate(_hideLayerSound);
                            Debug.Log("Hide positiveX");
                            for (int i = 0; i < height; i++) {
                                for (int j = 0; j < width; j++) {
                                    _hiddenVoxels.Push(_voxels[_currentLayerNegX, i, j]);
                                    ChangeVoxelVisible(_voxels[_currentLayerNegX, i, j], false);
                                }
                            }
                            _currentLayerNegX++;
                        }
                    } else if ((vrShowLayer || Input.GetKeyDown(KeyCode.Alpha0)) && _hiddenVoxels.Count > 0) {
                        if (CheckIfFaceChanged("negX")) {
                            ResetAllLayers();
                        } else {
                            Instantiate(_showLayerSound);
                            Debug.Log("Show positiveX");
                            for (int i = 0; i < height * width; i++) {
                                GameObject tempVoxel = _hiddenVoxels.Pop();
                                ChangeVoxelVisible(tempVoxel, true);
                            }
                            _currentLayerNegX--;
                        }
                    }
                    break;
                case "positiveY":
                    if ((vrHideLayer || Input.GetKeyDown(KeyCode.Alpha9)) && _currentLayerPosY > 0) {
                        if (CheckIfFaceChanged("posY")) {
                            ResetAllLayers();
                        } else {
                            Instantiate(_hideLayerSound);
                            Debug.Log("Hide positiveY");
                            for (int i = 0; i < length; i++) {
                                for (int j = 0; j < width; j++) {
                                    _hiddenVoxels.Push(_voxels[i, _currentLayerPosY, j]);
                                    ChangeVoxelVisible(_voxels[i, _currentLayerPosY, j], false);
                                }
                            }
                            _currentLayerPosY--;
                        }
                    } else if ((vrShowLayer || Input.GetKeyDown(KeyCode.Alpha0)) && _hiddenVoxels.Count > 0) {
                        if (CheckIfFaceChanged("posY")) {
                            ResetAllLayers();
                        } else {
                            Instantiate(_showLayerSound);
                            Debug.Log("Show positiveY");
                            for (int i = 0; i < length * width; i++) {
                                GameObject tempVoxel = _hiddenVoxels.Pop();
                                ChangeVoxelVisible(tempVoxel, true);
                            }
                            _currentLayerPosY++;
                        }
                    }
                    break;
                case "negativeY":
                    if ((vrHideLayer || Input.GetKeyDown(KeyCode.Alpha9)) && _currentLayerNegY < height - 1) {
                        if (CheckIfFaceChanged("negY")) {
                            ResetAllLayers();
                        } else {
                            Instantiate(_hideLayerSound);
                            Debug.Log("Hide negativeY");
                            for (int i = 0; i < length; i++) {
                                for (int j = 0; j < width; j++) {
                                    _hiddenVoxels.Push(_voxels[i, _currentLayerNegY, j]);
                                    ChangeVoxelVisible(_voxels[i, _currentLayerNegY, j], false);
                                }
                            }
                            _currentLayerNegY++;
                        }
                    } else if ((vrShowLayer || Input.GetKeyDown(KeyCode.Alpha0)) && _hiddenVoxels.Count > 0) {
                        if (CheckIfFaceChanged("negY")) {
                            ResetAllLayers();
                        } else {
                            Instantiate(_showLayerSound);
                            Debug.Log("Show negativeY");
                            for (int i = 0; i < length * width; i++) {
                                GameObject tempVoxel = _hiddenVoxels.Pop();
                                ChangeVoxelVisible(tempVoxel, true);
                            }
                            _currentLayerNegY--;
                        }
                    }
                    break;
                case "positiveZ":
                    if ((vrHideLayer || Input.GetKeyDown(KeyCode.Alpha9)) && _currentLayerPosZ > 0) {
                        if (CheckIfFaceChanged("posZ")) {
                            ResetAllLayers();
                        } else {
                            Instantiate(_hideLayerSound);
                            Debug.Log("Hide positiveZ");
                            for (int i = 0; i < length; i++) {
                                for (int j = 0; j < height; j++) {
                                    _hiddenVoxels.Push(_voxels[i, j, _currentLayerPosZ]);
                                    ChangeVoxelVisible(_voxels[i, j, _currentLayerPosZ], false);
                                }
                            }
                            _currentLayerPosZ--;
                        }
                    } else if ((vrShowLayer || Input.GetKeyDown(KeyCode.Alpha0)) && _hiddenVoxels.Count > 0) {
                        if (CheckIfFaceChanged("posZ")) {
                            ResetAllLayers();
                        } else {
                            Instantiate(_showLayerSound);
                            Debug.Log("Show positiveY");
                            for (int i = 0; i < length * height; i++) {
                                GameObject tempVoxel = _hiddenVoxels.Pop();
                                ChangeVoxelVisible(tempVoxel, true);
                            }
                            _currentLayerPosZ++;
                        }
                    }
                    break;
                case "negativeZ":
                    if ((vrHideLayer || Input.GetKeyDown(KeyCode.Alpha9)) && _currentLayerNegZ < width - 1) {
                        if (CheckIfFaceChanged("negZ")) {
                            ResetAllLayers();
                        } else {
                            Instantiate(_hideLayerSound);
                            Debug.Log("Hide negitiveZ");
                            for (int i = 0; i < length; i++) {
                                for (int j = 0; j < height; j++) {
                                    _hiddenVoxels.Push(_voxels[i, j, _currentLayerNegZ]);
                                    ChangeVoxelVisible(_voxels[i, j, _currentLayerNegZ], false);
                                }
                            }
                            _currentLayerNegZ++;
                        }
                    } else if ((vrShowLayer || Input.GetKeyDown(KeyCode.Alpha0)) && _hiddenVoxels.Count > 0) {
                        if (CheckIfFaceChanged("negZ")) {
                            ResetAllLayers();
                        } else {
                            Instantiate(_showLayerSound);
                            Debug.Log("Show negitiveY");
                            for (int i = 0; i < length * height; i++) {
                                GameObject tempVoxel = _hiddenVoxels.Pop();
                                ChangeVoxelVisible(tempVoxel, true);
                            }
                            _currentLayerNegZ--;
                        }
                    }
                    break;
            }
        }
    }

    private void GetNearestFace()
    {
        CalculateFaceCenters();
        float positiveXDistance = Vector3.Distance(_mainCamera.transform.position, _cubeFaceCenterCoords["positiveX"]);
        float negativeXDistance = Vector3.Distance(_mainCamera.transform.position, _cubeFaceCenterCoords["negativeX"]);
        float positiveYDistance = Vector3.Distance(_mainCamera.transform.position, _cubeFaceCenterCoords["positiveY"]);
        float negativeYDistance = Vector3.Distance(_mainCamera.transform.position, _cubeFaceCenterCoords["negativeY"]);
        float positiveZDistance = Vector3.Distance(_mainCamera.transform.position, _cubeFaceCenterCoords["positiveZ"]);
        float negativeZDistance = Vector3.Distance(_mainCamera.transform.position, _cubeFaceCenterCoords["negativeZ"]);

        float minimumDistance = 1000f;
        if (positiveXDistance < minimumDistance)
        {
            minimumDistance = positiveXDistance;
            _nearestFace = "positiveX";
        }
        if (negativeXDistance < minimumDistance)
        {
            minimumDistance = negativeXDistance;
            _nearestFace = "negativeX";
        }
        if (positiveYDistance < minimumDistance)
        {
            minimumDistance = positiveYDistance;
            _nearestFace = "positiveY";
        }
        if (negativeYDistance < minimumDistance)
        {
            minimumDistance = negativeYDistance;
            _nearestFace = "negativeY";
        }
        if (positiveZDistance < minimumDistance)
        {
            minimumDistance = positiveZDistance;
            _nearestFace = "positiveZ";
        }
        if (negativeZDistance < minimumDistance)
        {
            minimumDistance = negativeZDistance;
            _nearestFace = "negativeZ";
        }
    }

    private void CalculateFaceCenters()
    {
        _cubeFaceCenterCoords["negativeX"] = transform.position + transform.right * (length * cube.transform.localScale.x / 2);
        _cubeFaceCenterCoords["positiveX"] = transform.position + transform.right * (-length * cube.transform.localScale.x / 2);
        _cubeFaceCenterCoords["positiveY"] = transform.position + transform.up * (height * cube.transform.localScale.y / 2);
        _cubeFaceCenterCoords["negativeY"] = transform.position + transform.up * (-height * cube.transform.localScale.y / 2);
        _cubeFaceCenterCoords["negativeZ"] = transform.position + transform.forward * (width * cube.transform.localScale.z / 2);
        _cubeFaceCenterCoords["positiveZ"] = transform.position + transform.forward * (-width * cube.transform.localScale.z / 2);

        Debug.DrawLine(_mainCamera.transform.position, _cubeFaceCenterCoords["positiveX"], Color.red);
        Debug.DrawLine(_mainCamera.transform.position, _cubeFaceCenterCoords["negativeX"], Color.red);
        Debug.DrawLine(_mainCamera.transform.position, _cubeFaceCenterCoords["positiveY"], Color.red);
        Debug.DrawLine(_mainCamera.transform.position, _cubeFaceCenterCoords["negativeY"], Color.red);
        Debug.DrawLine(_mainCamera.transform.position, _cubeFaceCenterCoords["positiveZ"], Color.red);
        Debug.DrawLine(_mainCamera.transform.position, _cubeFaceCenterCoords["negativeZ"], Color.red);
    }

    private void ManageMode()
    {
        float switchModeFloat = SteamVR_Actions.picross.SwitchModeFloat[SteamVR_Input_Sources.Any].axis;
        bool switchModeDesktop = Input.GetKeyDown(KeyCode.Space);

        bool switchMode = switchModeFloat > 0.9f;

        if (switchMode || switchModeDesktop)
        {
            if (modeSwitchable)
            {
                Instantiate(_switchModeSound);
                StartCoroutine(SwitchModeCoroutine());
            }
        }
    }

    private IEnumerator SwitchModeCoroutine()
    {
        modeSwitchable = false;
        if (_currentGameMode == GameMode.Mark)
        {
            GameObject.FindGameObjectWithTag("Mark").GetComponent<ChangeModeSelector>().selected = false;
            GameObject.FindGameObjectWithTag("Destroy").GetComponent<ChangeModeSelector>().selected = true;
            GameObject.FindGameObjectWithTag("Mark").GetComponent<MeshRenderer>().material = _unselectedModeSelectorMat;
            GameObject.FindGameObjectWithTag("Destroy").GetComponent<MeshRenderer>().material = _selectedModeSelectorMat;
            _currentGameMode = GameMode.Destroy;
            MakeDestroyable();
        }
        else if (_currentGameMode == GameMode.Destroy) {
            GameObject.FindGameObjectWithTag("Mark").GetComponent<ChangeModeSelector>().selected = true;
            GameObject.FindGameObjectWithTag("Destroy").GetComponent<ChangeModeSelector>().selected = false;
            GameObject.FindGameObjectWithTag("Mark").GetComponent<MeshRenderer>().material = _selectedModeSelectorMat;
            GameObject.FindGameObjectWithTag("Destroy").GetComponent<MeshRenderer>().material = _unselectedModeSelectorMat;
            _currentGameMode = GameMode.Mark;
            MakeMarkable();
        } 
        else if (_currentGameMode == GameMode.Build) {
            GameObject.FindGameObjectWithTag("Mark").GetComponent<ChangeModeSelector>().selected = true;
            GameObject.FindGameObjectWithTag("Build").GetComponent<ChangeModeSelector>().selected = false;
            GameObject.FindGameObjectWithTag("Mark").GetComponent<MeshRenderer>().material = _selectedModeSelectorMat;
            GameObject.FindGameObjectWithTag("Build").GetComponent<MeshRenderer>().material = _unselectedModeSelectorMat;
            _currentGameMode = GameMode.Mark;
            MakeMarkable();
        }
        yield return new WaitForSeconds(0.25f);
        modeSwitchable = true;
    }

    public void MakeBuildable()
    {
        for (int i = 0; i < length; i++)
        {
            for (int j = 0; j < height; j++)
            {
                for (int k = 0; k < width; k++)
                {
                    GameObject currentVoxel = _voxels[i, j, k];
                    if (!currentVoxel.GetComponent<Voxel>().IsVisible)
                    {
                        currentVoxel.SetActive(true);
                        currentVoxel.GetComponent<MeshRenderer>().material = _clearMaterial;
                        currentVoxel.GetComponent<Voxel>().IsHovering = false;
                    }
                }
            }
        }
    }

    public void MakeDestroyable()
    {
        for (int i = 0; i < length; i++)
        {
            for (int j = 0; j < height; j++)
            {
                for (int k = 0; k < width; k++)
                {
                    GameObject currentVoxel = _voxels[i, j, k];
                    if (!currentVoxel.GetComponent<Voxel>().IsVisible)
                    {
                        currentVoxel.SetActive(false);
                        currentVoxel.GetComponent<Voxel>().IsHovering = false;
                    }
                }
            }
        }
    }

    public void MakeMarkable()
    {
        for (int i = 0; i < length; i++)
        {
            for (int j = 0; j < height; j++)
            {
                for (int k = 0; k < width; k++)
                {
                    GameObject currentVoxel = _voxels[i, j, k];
                    if (!currentVoxel.GetComponent<Voxel>().IsVisible)
                    {
                        currentVoxel.SetActive(false);
                        currentVoxel.GetComponent<Voxel>().IsHovering = false;
                    }
                }
            }
        }
    }

    private void CreateSolution()
    {
        _solution = new VoxelState[length, height, width];

        for (int i = 0; i < length; i++)
        {
            for (int j = 0; j < height; j++)
            {
                for (int k = 0; k < width; k++)
                {
                    _solution[i, j, k] = Convert.ToBoolean(Random.Range(0, 2)) ? VoxelState.Cleared : VoxelState.Marked;
                }
            }
        }

        Debug.Log("Actual Sol:");
        PrintSolution(_solution);
    }

    private void PrintSolution(VoxelState[,,] solution)
    {
        string matrices = "Solution:";
        for (int k = width - 1; k >= 0; k--)
        {
            matrices += $"\n Layer {k + 1}: \n";
            for (int j = height - 1; j >= 0; j--)
            {
                matrices += "| ";
                for (int i = 0; i < length; i++)
                {
                    char toAdd = 'u';
                    if (solution[i, j, k] != VoxelState.Unmarked)
                        toAdd = solution[i, j, k] == VoxelState.Marked ? '1' : '0';
                    matrices += $"{toAdd} ";
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

                for (int k = 0; k < width; k++)
                {
                    if (_solution[i, j, k] == VoxelState.Marked)
                    {
                        voxelCount++;
                    }
                    else if (k != width - 1 && voxelCount > 0 && _solution[i, j, k] == VoxelState.Cleared && _solution[i, j, k + 1] == VoxelState.Marked)
                    {
                        gapCount++;
                    }

                    if (k == width - 1 && voxelCount <= 1)
                    {
                        gapCount = 0;
                    }
                }

                _frontClues[i, j] = new Clue(blank: false, voxelCount: voxelCount, gapCount: gapCount);
                _voxels[i, j, 0].GetComponent<Voxel>().SetSideText(VoxelSide.Front, voxelCount, gapCount);
                _voxels[i, j, width - 1].GetComponent<Voxel>().SetSideText(VoxelSide.Rear, voxelCount, gapCount);
            }
        }

        // Populate side clues
        for (int j = 0; j < height; j++)
        {
            for (int k = 0; k < width; k++)
            {
                int voxelCount = 0;
                int gapCount = 0;

                for (int i = 0; i < length; i++)
                {
                    if (_solution[i, j, k] == VoxelState.Marked)
                    {
                        voxelCount++;
                    }
                    else if (i != length - 1 && voxelCount > 0 && _solution[i, j, k] == VoxelState.Cleared && _solution[i + 1, j, k] == VoxelState.Marked)
                    {
                        gapCount++;
                    }

                    if (i == length - 1 && voxelCount <= 1)
                    {
                        gapCount = 0;
                    }
                }

                _sideClues[k, j] = new Clue(blank: false, voxelCount: voxelCount, gapCount: gapCount);
                _voxels[0, j, k].GetComponent<Voxel>().SetSideText(VoxelSide.LeftSide, voxelCount, gapCount);
                _voxels[length - 1, j, k].GetComponent<Voxel>().SetSideText(VoxelSide.RightSide, voxelCount, gapCount);
            }
        }

        // populate top clues
        for (int k = 0; k < width; k++)
        {
            for (int i = 0; i < length; i++)
            {
                int voxelCount = 0;
                int gapCount = 0;

                for (int j = 0; j < height; j++)
                {
                    if (_solution[i, j, k] == VoxelState.Marked)
                    {
                        voxelCount++;
                    }
                    else if (j != height - 1 && voxelCount > 0 && _solution[i, j, k] == VoxelState.Cleared && _solution[i, j + 1, k] == VoxelState.Marked)
                    {
                        gapCount++;
                    }

                    if (j == height - 1 && voxelCount <= 1)
                    {
                        gapCount = 0;
                    }
                }

                _topClues[i, k] = new Clue(blank: false, voxelCount: voxelCount, gapCount: gapCount);
                _voxels[i, 0, k].GetComponent<Voxel>().SetSideText(VoxelSide.Bottom, voxelCount, gapCount);
                _voxels[i, height - 1, k].GetComponent<Voxel>().SetSideText(VoxelSide.Top, voxelCount, gapCount);
            }
        }

        string frontCluesString = "front clues:\n";
        for (int j = height - 1; j >= 0; j--)
        {
            frontCluesString += "| ";
            for (int i = length - 1; i >= 0; i--)
            {
                frontCluesString += $"({_frontClues[i, j].VoxelCount}^{_frontClues[i, j].GapCount}) ";
            }

            frontCluesString += "|\n";
        }

        Debug.Log(frontCluesString);

        string sideCluesString = "side clues:\n";
        for (int j = height - 1; j >= 0; j--)
        {
            sideCluesString += "| ";
            for (int k = width - 1; k >= 0; k--)
            {
                sideCluesString += $"({_sideClues[k, j].VoxelCount}^{_sideClues[k, j].GapCount}) ";
            }

            sideCluesString += "|\n";
        }

        Debug.Log(sideCluesString);

        string topCluesString = "top clues:\n";
        for (int k = 0; k < width; k++)
        {
            topCluesString += "| ";
            for (int i = length - 1; i >= 0; i--)
            {
                topCluesString += $"({_topClues[i, k].VoxelCount}^{_topClues[i, k].GapCount}) ";
            }

            topCluesString += "|\n";
        }

        Debug.Log(topCluesString);
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
        Voxel voxelVoxel = voxel.GetComponent<Voxel>();
        bool grow = true;
        Vector3 normalSize = cube.transform.localScale;
        if (voxelVoxel.IsVisible)
        {
            Debug.Log("It's visible!");
        }
        if (voxelVoxel.IsVisible)
        {
            voxel.SetActive(true);
        }
        while (grow)
        {
            if (voxel.transform.localScale != normalSize)
            {
                voxel.transform.localScale += new Vector3(0.025f, 0.025f, 0.025f);
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
        Vector3 smallSize = Vector3.zero;
        while (shrink)
        {
            if (voxel.transform.localScale != smallSize)
            {
                voxel.transform.localScale -= new Vector3(0.025f, 0.025f, 0.025f);
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
        Vector2 controllerRotation = SteamVR_Actions.picross.Rotate[SteamVR_Input_Sources.Any].axis;

        float horizontalMovement = SteamVR_Actions.picross.Rotate[SteamVR_Input_Sources.Any].axis.x;
        float verticalMovement = SteamVR_Actions.picross.Rotate[SteamVR_Input_Sources.Any].axis.y;

        bool rotateRight = false;
        bool rotateLeft = false;
        bool rotateUp = false;
        bool rotateDown = false;

        if (horizontalMovement >= .65f)
        {
            rotateRight = true;
        }
        if (horizontalMovement <= -.65f)
        {
            rotateLeft = true;
        }
        if (verticalMovement >= .65f)
        {
            rotateUp = true;
        }
        if (verticalMovement <= -.65f)
        {
            rotateDown = true;
        }


        float timeSpeed = rotateSpeed * Time.deltaTime;

        AudioSource audioSource = GetComponent<AudioSource>();

        if (_canEditPuzzle)
        {
            bool rotating = false;
            if (Input.GetKey(KeyCode.J) || rotateLeft)
            {
                transform.RotateAround(_target, Vector3.up, timeSpeed);
                rotating = true;
            }

            if (Input.GetKey(KeyCode.L) || rotateRight)
            {
                transform.RotateAround(_target, Vector3.up, -timeSpeed);
                rotating = true;
            }

            if (Input.GetKey(KeyCode.I) || rotateUp && _canVerticallyRotate)
            {
                transform.RotateAround(_target, transform.right, -timeSpeed);
                rotating = true;
            }

            if (Input.GetKey(KeyCode.K) || rotateDown && _canVerticallyRotate)
            {
                transform.RotateAround(_target, transform.right, timeSpeed);
                rotating = true;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                transform.rotation = Quaternion.identity;
            }

            if (rotating && !audioSource.isPlaying)
            {
                audioSource.Play();
            } else if (!rotating)
            {
                audioSource.Stop();
            }
        }
        else
        {
            if ((transform.rotation.eulerAngles.x > 2 && transform.rotation.eulerAngles.x < 358) || 
                (transform.rotation.eulerAngles.z > 1 && transform.rotation.eulerAngles.z < 358))
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.identity, timeSpeed / 3);
            }
            else
            {
                transform.RotateAround(_target, transform.up, timeSpeed / 3);
            }
        }
    }
    
    private VoxelInfo[,,] SetPuzzleVoxels(UInt32[] palette, byte[] flatData, Int32 sizeX, Int32 sizeY, Int32 sizeZ)
    {
        VoxelInfo[,,] solution = new VoxelInfo[sizeX, sizeY, sizeZ];
        Color[] colorPalette = new Color[palette.Length];

        // Parse colors
        byte a, r, g, b;
        for (int i = 0; i < palette.Length; i++)
        {
            palette[i].ToARGB(out a, out r, out g, out b);
            colorPalette[i] = new Color(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f);
        }
        
        // Convert 1D array to 3D
        byte[,,] data = new byte[sizeX, sizeY, sizeZ];
        for (int i = 0; i < sizeX; i++)
        {
            for (int j = 0; j < sizeY; j++)
            {
                for (int k = 0; k < sizeZ; k++)
                {
                    data[i, j, k] = flatData[k + j * sizeZ + i * sizeY * sizeZ];
                }
            }
        }

        // Parse puzzle structure
        for (int i = 0; i < sizeX; i++)
        {
            for (int j = 0; j < sizeY; j++)
            {
                for (int k = 0; k < sizeZ; k++)
                {
                    bool partOfSolution = data[i, j, k] != 0;

                    solution[i, j, k] = new VoxelInfo(
                        partOfSolution ? VoxelState.Marked : VoxelState.Cleared,
                        colorPalette[data[i, j, k]],
                        new Vector3Int(i, j, k));
                }
            }
        }

        return solution;
    }
    
    private VoxelState[,,] GetSolutionVoxelStates(VoxelInfo[,,] voxelInfoSolution)
    {
        int sizeX = voxelInfoSolution.GetLength(0);
        int sizeY = voxelInfoSolution.GetLength(1);
        int sizeZ = voxelInfoSolution.GetLength(2);

        VoxelState[,,] _states = new VoxelState[sizeX, sizeY, sizeZ];

        for (int i = 0; i < sizeX; i++)
        {
            for (int j = 0; j < sizeY; j++)
            {
                for (int k = 0; k < sizeZ; k++)
                {
                    _states[i, j, k] = voxelInfoSolution[i, j, k].VoxelState;
                }
            }
        }

        return _states;
    }
}