using DavidFDev.DevConsole;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Yarn.Unity;

public class YarnCommandManager : MonoBehaviour {
    private void Awake() {
        DevConsole.EnableConsole();
        DevConsole.SetToggleKey(KeyCode.Escape);

        DevConsole.AddCommand(Command.Create(
            name: "reload_active_scene",
            aliases: "r",
            helpText: "Reloads active scene",
            callback: () => {
                DevConsole.CloseConsole();
                SceneManager.LoadScene(0);
            }));
    }
}
