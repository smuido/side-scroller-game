using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ProjectFolderSetup
{
    // Menu item in Unity Editor
    [MenuItem("Tools/Project Setup/Create Default Folders")]
    public static void CreateDefaultFolders()
    {
        try
        {
            string root = "_PROJECT_";
            
            // Create folder to hold modified Monobehaviour script template
            string templateFolder = "ScriptTemplates";
            
            string templateFolderFullPath = Path.Combine(Application.dataPath, templateFolder);
            
            if(!Directory.Exists(templateFolderFullPath))
            {
                Directory.CreateDirectory(templateFolderFullPath);
                Debug.Log("Created ScriptTemplates folder: " + templateFolderFullPath);
            }
            
            string newMonobehaviourTemplatePath = Path.Combine(templateFolderFullPath, "1-Scripting__Haungs MonoBehaviour Script-NewMonoBehaviourScript.cs.txt");
            if (!File.Exists(newMonobehaviourTemplatePath))
            {
                string content =
@"
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class #SCRIPTNAME# : MonoBehaviour
{
    // Fields
    #NOTRIM#
    // Properties
    #NOTRIM#
    // Events / Delegates
    #NOTRIM#
    // Monobehaviour Methods (Awake, Start, Update, ...)
    #NOTRIM#
    void Update()
    {
        #NOTRIM#
    }
    #NOTRIM#
    // Public Methods
    #NOTRIM#
    // Private Methods
    #NOTRIM#
}
";

                File.WriteAllText(newMonobehaviourTemplatePath, content);
            }

            // List of folders to create under Assets/_PROJECT_ 
            string[] folders =
            {
                "Animations",
                "Audio",
                "Editor",
                "Materials",
                "Meshes",
                "Prefabs",
                "Scripts",
                "Scenes",
                "Shaders",
                "Textures",
                "UI"
            };

            string rootFullPath = Path.Combine(Application.dataPath, root);

            // Create each folder safely
            foreach (var folder in folders)
            {
                string fullPath = Path.Combine(rootFullPath, folder);

                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    Debug.Log("Created folder: " + fullPath);
                }
            }
            
            string[] uiFolders =
            {
                "Assets",
                "Fonts",
                "Icon"
            };

            foreach (var subfolder in uiFolders)
            {
                if (!Directory.Exists("Assets/" + root + "/UI/" + subfolder))
                {
                    Directory.CreateDirectory("Assets/" + root + "/UI/" + subfolder);
                }
            }

            string readmePath = Path.Combine(Application.dataPath, "README - Folder Structure.txt");
            if (!File.Exists(readmePath))
            {
                string content =
                    "CSC 3710 Folder Structure:\n\n" +
                    "In this class, we will use a common project folder structure to:\n" +
                    "\t(1) Organize assets in a logical manner.\n" +
                    "\t(2) Facilitate collaboration among team members.\n" +
                    "\t(3) Allow the instructor to easily navigate the project.\n" +
                    "\t(4) Ensure consistent project management.";

                File.WriteAllText(readmePath, content);
            }

            // Update the Project window
            AssetDatabase.Refresh();

            Debug.Log("Default project folders created successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError("Error creating folders: " + e.Message);
        }
    }
}