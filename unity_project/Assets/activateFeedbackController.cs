using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Neuron;

public class activateFeedbackController : MonoBehaviour
{
    public enum FeedbackMode
    {
        Backhand,
        Forehand
    }

    [SerializeField] private FeedbackMode feedbackMode = FeedbackMode.Backhand;

    [Tooltip("One shared replay engine. The selected feedback mode only changes the stroke-specific EMG channel mapping.")]
    [SerializeField] private IMPACTReplay replay;

    [Tooltip("Retrieves the single expert stroke selected from the recorded user swing by the AutoEncoder service.")]
    [SerializeField] private GenerateExpert expertMatcher;

    [Header("Muscle configuration")]
    [Tooltip("Stroke-specific visual retained under one Muscle Configuration parent.")]
    [SerializeField] private GameObject forehandConfiguration;
    [SerializeField] private GameObject backhandConfiguration;

    [SerializeField] private GameObject startbuttonText; // "Start" 텍스트
    [SerializeField] private GameObject stopbuttonText;  // "Stop" 텍스트
    [SerializeField] private GameObject StartbuttonImage; // "Start" 버튼
    [SerializeField] private GameObject StopbuttonImage;  // "Stop" 버튼

    private bool isActivated = false; // false = Start 상태, true = Stop 상태

    void Start()
    {
        // Preserve the scene reference when serialized, but recover safely
        // when Unity runs a temporary backup scene during recompilation.
        if (expertMatcher == null)
            expertMatcher = FindObjectOfType<GenerateExpert>();

        SetButtonState(isActivated);
        SetMuscleConfiguration(feedbackMode);

        if (replay != null)
        {
            replay.OnReplayFinished += HandleReplayFinished;
        }
    }

    private void HandleReplayFinished()
    {
        isActivated = false;
        SetButtonState(isActivated);
    }

    public void onActivateFeedbackButtonClicked()
    {
        if (replay == null)
        {
            Debug.LogError("Replay controller is not assigned.");
            return;
        }

        if (!isActivated)
        {
            // One replay engine drives both conditions. Only the displayed
            // stroke-relevant EMG channels differ; all timing and UI behaviour
            // stays identical.
            replay.isForehand = feedbackMode == FeedbackMode.Forehand;
            SetMuscleConfiguration(feedbackMode);

            if (expertMatcher == null)
            {
                expertMatcher = FindObjectOfType<GenerateExpert>();
            }

            if (expertMatcher == null)
            {
                Debug.LogError("AutoEncoder matcher is not present in this scene. Replay cannot select an expert automatically.");
                SetButtonState(false);
                return;
            }

            if (!expertMatcher.RequestMatchedExpert(replay.subject, replay.strokeNumber))
            {
                Debug.LogWarning("Replay was not started because automatic expert retrieval failed.");
                SetButtonState(false);
                return;
            }

            replay.ActivateReplay();

            isActivated = replay.IsReplaying;

            if (!isActivated)
            {
                Debug.LogWarning("Replay was not started; keeping the feedback button in its Start state.");
                SetButtonState(false);
                return;
            }
        }
        else
        {
            replay.StopReplay();

            isActivated = false;
        }

        SetButtonState(isActivated);
    }

    private void SetMuscleConfiguration(FeedbackMode mode)
    {
        if (forehandConfiguration != null)
            forehandConfiguration.SetActive(mode == FeedbackMode.Forehand);

        if (backhandConfiguration != null)
            backhandConfiguration.SetActive(mode == FeedbackMode.Backhand);
    }

    private void SetButtonState(bool isActivated)
    {
        if (!isActivated)
        {
            if (StartbuttonImage != null) StartbuttonImage.SetActive(true);
            if (startbuttonText != null) startbuttonText.SetActive(true);

            if (StopbuttonImage != null) StopbuttonImage.SetActive(false);
            if (stopbuttonText != null) stopbuttonText.SetActive(false);
        }
        else
        {
            if (StartbuttonImage != null) StartbuttonImage.SetActive(false);
            if (startbuttonText != null) startbuttonText.SetActive(false);

            if (StopbuttonImage != null) StopbuttonImage.SetActive(true);
            if (stopbuttonText != null) stopbuttonText.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        if (replay != null)
            replay.OnReplayFinished -= HandleReplayFinished;
    }
}
