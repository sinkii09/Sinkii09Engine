using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Services;
using Sirenix.OdinInspector;
using UnityEngine;

public class Mono_Test : MonoBehaviour
{
    [SerializeField] string testActorId = "aria";

    [SerializeField] ActorType testActorType = ActorType.Character;
    [SerializeField] CharacterEmotion testEmotion = CharacterEmotion.Happy;
    [SerializeField] CharacterPose testPose = CharacterPose.Standing;
    [SerializeField] CharacterLookDirection testLookDirection = CharacterLookDirection.Center;
    [SerializeField] CharacterPosition testPosition = CharacterPosition.Center;
    [SerializeField] Color testColor = Color.white;
    [SerializeField] float testScale = 1f;

    [Title("Actor Service Tests")]
    [Button("Test Actor Service")]
    public void ShowActor()
    {
        var showCmd = new ShowActorCommand
        {
            actorId = testActorId,
            actorType = testActorType.ToString(),
            expression = testEmotion.ToString(),
            pose = testPose.ToString(),
            tintColor = testColor.ToString(),
        };

        showCmd.ExecuteAsync().Forget();
    }

    [Button("Test Actor Service - Change Appearance")]
    public void ChangeApperance()
    {
        var scaleCmd = new ChangeActorAppearanceCommand
        {
            actorId = testActorId,
            expression = testEmotion.ToString(),
            pose = testPose.ToString(),
            lookDirection = testLookDirection.ToString()
        };
        scaleCmd.ExecuteAsync().Forget();
    }

    [Button("Test Actor Service - Move Actor")]
    public void MoveActor()
    {
        var moveCmd = new MoveActorCommand
        {
            actorId = testActorId,
            position = testPosition.ToString(),
            scale = testScale.ToString(),
            duration = 1f
        };
        moveCmd.ExecuteAsync().Forget();
    }
}
