using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Simple singleton dialogue manager for NPC conversations.
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [Header("UI References")]
        public TextMeshProUGUI speakerNameText;
        public TextMeshProUGUI dialogueText;
        public GameObject dialoguePanel;

        [Header("Settings")]
        [Tooltip("When disabled, the active interaction system advances or closes dialogue from player input.")]
        public bool autoAdvance;
        public float autoAdvanceDelay = 3f;

        private float _timer;
        private bool _isShowing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_isShowing || !autoAdvance) return;

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                Hide();
            }
        }

        public void ShowLine(string speaker, string line)
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(true);
            if (speakerNameText != null) speakerNameText.text = speaker;
            if (dialogueText != null) dialogueText.text = line;
            _isShowing = true;
            _timer = autoAdvanceDelay;
        }

        public void Hide()
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            _isShowing = false;
        }
    }
}
