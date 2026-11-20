using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

[RequireComponent(typeof(CharacterController))]
public class CharacterManager : MonoBehaviour
{
    public UnityEvent<float> OnDeath;
    public bool IsEnabled { get; set; }
    public bool IsInputEnabled { get; set; }
    public float DistanceTravelled { get; private set; }

    [SerializeField]
    private float _forwardSpeed = 10.0f;

    [SerializeField]
    private float _lateralSpeed = 5.0f;

    [SerializeField]
    private float _rotationSpeed = 5.0f;

    [SerializeField]
    private Animator _animator;

    [SerializeField]
    private List<Transform> _lanesSToSW;

    private int _currentLaneIndex;

    // Touch/swipe lane switching for WebGL mobile players.
    // A horizontal swipe toggles direction based on sign of X delta.
    [SerializeField]
    private float _swipeThresholdPixels = 60f;

    private float _touchStartX;
    private bool _touchActive;

    private CharacterController _characterController;

    public static CharacterManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        IsEnabled = true;
        _currentLaneIndex = 0;
        BlockchainManager.Instance.OnLoggedIn.AddListener((address) => IsInputEnabled = true);
    }

    private void Update()
    {
        if (IsEnabled)
        {
            HandleInput();
            Move();
            Rotate();
        }
    }

    private void HandleInput()
    {
        if (IsInputEnabled)
        {
            DistanceTravelled += _forwardSpeed * Time.deltaTime;

            // Handle lane switching — keyboard
            if (Input.GetKeyDown(KeyCode.A))
            {
                SwitchLane(-1);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                SwitchLane(1);
            }

            HandleTouchInput();
        }
    }

    private void SwitchLane(int direction)
    {
        _currentLaneIndex += direction;
        if (_currentLaneIndex < 0)
            _currentLaneIndex = _lanesSToSW.Count - 1;
        if (_currentLaneIndex >= _lanesSToSW.Count)
            _currentLaneIndex = 0;
        _animator.SetTrigger(direction < 0 ? "Roll_Left" : "Roll_Right");
    }

    private void HandleTouchInput()
    {
        // Swipe left/right anywhere on screen to change lanes.
        // Works for both raw touches (mobile) and WebGL mouse-drag as a
        // fallback so desktop players can use it too.
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                _touchStartX = touch.position.x;
                _touchActive = true;
            }
            else if (touch.phase == TouchPhase.Ended && _touchActive)
            {
                float delta = touch.position.x - _touchStartX;
                if (Mathf.Abs(delta) >= _swipeThresholdPixels)
                    SwitchLane(delta > 0 ? 1 : -1);
                _touchActive = false;
            }
        }
        else if (Input.GetMouseButtonDown(0))
        {
            _touchStartX = Input.mousePosition.x;
            _touchActive = true;
        }
        else if (Input.GetMouseButtonUp(0) && _touchActive)
        {
            float delta = Input.mousePosition.x - _touchStartX;
            if (Mathf.Abs(delta) >= _swipeThresholdPixels)
                SwitchLane(delta > 0 ? 1 : -1);
            _touchActive = false;
        }
    }

    private void Move()
    {
        // Target lane position with adjusted Y to match character's current Y position
        Vector3 targetPosition = new Vector3(
            _lanesSToSW[_currentLaneIndex].position.x,
            _lanesSToSW[_currentLaneIndex].position.y,
            _characterController.transform.position.z
        );

        // Smoothly interpolate to the target position
        Vector3 smoothedPosition = Vector3.Slerp(
            _characterController.transform.position,
            targetPosition,
            _lateralSpeed * Time.deltaTime
        );
        _characterController.Move(smoothedPosition - _characterController.transform.position);

        // Apply forward movement
        Vector3 forwardMovement = _forwardSpeed * Time.deltaTime * Vector3.forward;
        _characterController.Move(forwardMovement);
    }

    private void Rotate()
    {
        // Rotate player to match the lane's/wall's orientation
        Quaternion targetRotation = Quaternion.LookRotation(
            Vector3.forward,
            _lanesSToSW[_currentLaneIndex].up
        );
        _characterController.transform.rotation = Quaternion.Slerp(
            _characterController.transform.rotation,
            targetRotation,
            Time.deltaTime * _rotationSpeed
        );
    }

    public int GetLaneIndex()
    {
        return _currentLaneIndex;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle"))
        {
            IsEnabled = false;
            IsInputEnabled = false;
            // _animator.SetTrigger("Death");
            OnDeath?.Invoke(DistanceTravelled);
        }
    }
}
