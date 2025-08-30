using UnityEngine;
using Sinkii09.Engine.Services;
using Cysharp.Threading.Tasks;
using Sinkii09.Engine;

public class DialogueTest : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private float delayBetweenLines = 1f;
    
    private IDialogueService _dialogueService;
    
    private void Start()
    {
        if (autoStart)
        {
            _ = RunDialogueTestAsync();
        }
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            _ = RunDialogueTestAsync();
        }
    }
    
    private async UniTaskVoid RunDialogueTestAsync()
    {
        _dialogueService = Engine.GetService<IDialogueService>();
        if (_dialogueService == null)
        {
            Debug.LogError("DialogueService not found!");
            return;
        }
        
        // Test basic dialogue
        await _dialogueService.ShowDialogueAsync("Alice", "Hello! Welcome to our school!");
        await UniTask.Delay(System.TimeSpan.FromSeconds(delayBetweenLines));
        
        await _dialogueService.ShowDialogueAsync("Bob", "Nice to meet you!");
        await UniTask.Delay(System.TimeSpan.FromSeconds(delayBetweenLines));
        
        // Test narration
        await _dialogueService.ShowNarrationAsync("The two characters look at each other.");
        await UniTask.Delay(System.TimeSpan.FromSeconds(delayBetweenLines));
        
        // Test choices
        //var choices = new DialogueChoice[]
        //{
        //    new DialogueChoice("Hello Alice!", true),
        //    new DialogueChoice("Hi Bob!", true),
        //    new DialogueChoice("I have to go.", true)
        //};
        
        //var selectedChoice = await _dialogueService.ShowChoicesWithPromptAsync(
        //    "What do you say?", choices);
        
        //// Response based on choice
        //switch (selectedChoice)
        //{
        //    case 0:
        //        await _dialogueService.ShowDialogueAsync("Alice", "Thanks for greeting me!");
        //        break;
        //    case 1:
        //        await _dialogueService.ShowDialogueAsync("Bob", "Hey there!");
        //        break;
        //    case 2:
        //        await _dialogueService.ShowNarrationAsync("You wave goodbye and walk away.");
        //        break;
        //}
        
        Debug.Log("Dialogue test completed!");
    }
}