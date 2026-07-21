using UnityEngine;
using UnityEngine.UI;

public class DemoGameController : MonoBehaviour
{
    [SerializeField] private GameObject tutorialScreen;
    [SerializeField] private GameObject gameBoardScreen;
    [SerializeField] private Button continueButton;

    private void Awake()
    {
        continueButton.onClick.AddListener(OnContinue);
        gameBoardScreen.SetActive(false); // board hidden until tutorial done
    }

    private void OnContinue()
    {
        tutorialScreen.SetActive(false);
        gameBoardScreen.SetActive(true);
    }
}