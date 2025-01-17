using System.Collections;
using UnityEngine;
using Valve.VR;

public class TitleExitCube : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject _mainCamera;
    [SerializeField] private Material _defaultColor;
    [SerializeField] private Material _hoverColor;
    [SerializeField] private GameObject _sceneLoader;

    private MeshRenderer _meshRenderer;
    private Vector3 _initialPosition;
    private bool _isHovering = false;
    private float performActionFloat;
    private bool performAction;

    void Start()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
        _meshRenderer.material = _defaultColor;
        _initialPosition = transform.position;
    }

    void Update()
    {
        transform.Rotate(new Vector3(0, .5f, 0));

        if (_mainCamera != null)
        {
            float height = _mainCamera.transform.position.y;
            transform.position = _initialPosition + new Vector3(0, height - 0.25f, 0);
        }

        performActionFloat = SteamVR_Actions.picross.PerformActionFloat[SteamVR_Input_Sources.Any].axis;
        performAction = performActionFloat > 0.8f;
        if (_isHovering && performAction)
        {
            StartCoroutine(StartGame());
        }
    }

    private IEnumerator StartGame()
    {
        GameObject voxel = gameObject;
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
                shrink = false;
            }
            yield return new WaitForSeconds(0.06f);
        }

        Application.Quit();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Interactor"))
        {
            _isHovering = true;
            _meshRenderer.material = _hoverColor;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Interactor"))
        {
            _isHovering = false;
            _meshRenderer.material = _defaultColor;
        }
    }
}
