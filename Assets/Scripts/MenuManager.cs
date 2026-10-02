using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla o botao "Jogar" da tela inicial (menu). Coloque este script
/// num GameObject vazio na cena do menu e arraste o metodo StartGame()
/// pro OnClick do botao.
/// </summary>
public class MenuManager : MonoBehaviour
{
    [Tooltip("Nome exato da cena do jogo (precisa estar adicionada no Build Settings)")]
    public string gameSceneName = "SampleScene";

    public void StartGame()
    {
        // Fade preto suave em vez do corte seco - ver SceneTransition.cs.
        // Se por algum motivo o objeto de transicao nao existir na cena,
        // cai pro load direto (nunca trava o botao Jogar).
        if (SceneTransition.Instance != null)
        {
            SceneTransition.Instance.LoadScene(gameSceneName);
        }
        else
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }

    /// <summary>Fecha o jogo. No Editor apenas para o Play Mode, pra poder testar.</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
