using UnityEngine;
using Valve.VR;

public enum VoxelSide
{
    Front = 0,
    RightSide = 1,
    Top = 2,
    Rear = 3,
    LeftSide = 4,
    Bottom = 5
}

public class Voxel : MonoBehaviour
{
    [Header("Materials")] [SerializeField] private Material defaultColor;
    [SerializeField] private Material hoverColor;
    [SerializeField] private Material hoverDestroyColor;
    [SerializeField] private Material markedColor;
    [SerializeField] private Material clearColor;
    [SerializeField] private Material whiteColor;

    [SerializeField] private GameObject destroyParticles;
    [SerializeField] private GameObject paintParticles;

    [Header("Texts")] [SerializeField] private HintText frontHint;
    [SerializeField] private HintText rightSideHint;
    [SerializeField] private HintText topHint;
    [SerializeField] private HintText rearHint;
    [SerializeField] private HintText leftSideHint;
    [SerializeField] private HintText bottomHint;

    [SerializeField] private bool _isPuzzleVoxel;

    [Header("Sounds")]
    [SerializeField] private GameObject _buildSound;
    [SerializeField] private GameObject _destroySound;
    [SerializeField] private GameObject _markSound;

    public bool IsPuzzleVoxel
    {
        get => _isPuzzleVoxel;
        set => _isPuzzleVoxel = value;
    }

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set => _isVisible = value;
    }

    private bool _isMarked;
    public bool IsMarked
    {
        get => _isMarked;
        set => _isMarked = value;
    }

    private VoxelManager _manager;
    public VoxelManager Manager
    {
        get => _manager;
        set => _manager = value;
    }

    private bool _isHovering;
    public bool IsHovering
    {
        get => _isHovering;
        set => _isHovering = value;
    }

    private Vector3Int _indexPosition;
    public Vector3Int IndexPosition
    {
        get => _indexPosition;
        set => _indexPosition = value;
    }

    private HintText[] _hints = new HintText[6];
    public HintText[] Hints => _hints;
    
    private bool _hintArraySet;
    
    private MeshRenderer _meshRenderer;
    private bool performAction;
    private float performActionFloat;
    private bool canPerformAction;

    private void Start()
    {
        canPerformAction = true;
        _meshRenderer = GetComponent<MeshRenderer>();

        SetHintArray();
    }

    private void Update()
    {
        performActionFloat = SteamVR_Actions.picross.PerformActionFloat[SteamVR_Input_Sources.Any].axis;
        performAction = performActionFloat > 0.8f;

        if (canPerformAction && _isHovering && (Input.GetMouseButtonDown(0) || performAction))
        {
            if (_manager.CurrentGameMode == VoxelManager.GameMode.Build)
            {
                BuildVoxel();
                canPerformAction = false;
            }

            if (_manager.CurrentGameMode == VoxelManager.GameMode.Destroy)
            {
                ClearVoxel();
                canPerformAction = false;
            }

            if (_manager.CurrentGameMode == VoxelManager.GameMode.Mark)
            {
                MarkVoxel();
                canPerformAction = false;
            }
        }
    }

    public void ClearedPuzzle(Color completedColor)
    {
        foreach (HintText hint in _hints)
        {
            hint.gameObject.SetActive(false);
        }

        whiteColor.color = completedColor;
        _meshRenderer.material.SetColor("_BaseColor", completedColor);
    }

    private void BuildVoxel()
    {
        if (!_isVisible && _manager.CanEditPuzzle)
        {
            Instantiate(_buildSound);

            foreach (HintText hint in _hints)
            {
                hint.gameObject.SetActive(true);
            }
            
            _manager.UpdateAdjacentVoxelHints(_indexPosition);
            _manager.UpdateVoxelState(_indexPosition, _isMarked ? VoxelManager.VoxelState.Marked : VoxelManager.VoxelState.Unmarked);
            
            _isVisible = true;
            _meshRenderer.material = hoverColor;
        }
    }

    public void SetSideText(VoxelSide side, int voxelCount, int gapCount)
    {
        SetHintArray();
        _hints[(int) side].SetHintText(voxelCount, gapCount);
    }

    private void SetHintArray()
    {
        if (!_hintArraySet)
        {
            _hintArraySet = true;
            _hints[0] = frontHint;
            _hints[1] = rightSideHint;
            _hints[2] = topHint;
            _hints[3] = rearHint;
            _hints[4] = leftSideHint;
            _hints[5] = bottomHint;
        }
    }

    private void MarkVoxel()
    {
        if (_isVisible && _manager.CanEditPuzzle)
        {
            Instantiate(_markSound);
            if (_isMarked)
            {
                _isMarked = false;
                _meshRenderer.material = hoverColor;
                _manager.UpdateVoxelState(_indexPosition, VoxelManager.VoxelState.Unmarked);
            }
            else
            {
                _isMarked = true;
                _meshRenderer.material = markedColor;
                _manager.UpdateVoxelState(_indexPosition, VoxelManager.VoxelState.Marked);
                Instantiate(paintParticles, transform.position, Quaternion.identity);
            }
        }
    }

    private void ClearVoxel()
    {
        if (_manager.CanEditPuzzle)
        {
            Instantiate(_destroySound);
            foreach (HintText hint in _hints)
            {
                hint.gameObject.SetActive(!_isVisible);
            }

            if (!_isVisible)
            {
                _isVisible = true;
                _meshRenderer.material = hoverColor;
            }
            else
            {
                transform.gameObject.SetActive(false);
                _isVisible = false;
                _isMarked = false;
                _meshRenderer.material = clearColor;

                _manager.UpdateAdjacentVoxelHints(_indexPosition);
                _manager.UpdateVoxelState(_indexPosition, VoxelManager.VoxelState.Cleared);
                Instantiate(destroyParticles, transform.position, Quaternion.identity);
            }
        }
    }

    private void OnMouseEnter()
    {
        if (_manager.CanEditPuzzle)
        {
            _isHovering = true;
            if (_manager.CurrentGameMode == VoxelManager.GameMode.Destroy)
            {
                _meshRenderer.material = hoverDestroyColor;
            }
            else
            {
                _meshRenderer.material = hoverColor;
            }
        }
    }

    private void OnMouseExit()
    {
        if (_manager.CanEditPuzzle)
        {
            canPerformAction = true;
            _isHovering = false;
            if (!_isVisible)
            {
                _meshRenderer.material = clearColor;
            }
            else if (_isMarked)
            {
                _meshRenderer.material = markedColor;
            }
            else
            {
                _meshRenderer.material = defaultColor;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Interactor") && _manager.CanEditPuzzle)
        {
            canPerformAction = true;
            _isHovering = true;
            if (_manager.CurrentGameMode == VoxelManager.GameMode.Destroy)
            {
                _meshRenderer.material = hoverDestroyColor;
            }
            else
            {
                _meshRenderer.material = hoverColor;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Interactor") && _manager.CanEditPuzzle)
        {
            canPerformAction = true;
            _isHovering = false;
            if (!_isVisible)
            {
                _meshRenderer.material = clearColor;
            }
            else if (_isMarked)
            {
                _meshRenderer.material = markedColor;
            }
            else
            {
                _meshRenderer.material = defaultColor;
            }
        }
    }
}