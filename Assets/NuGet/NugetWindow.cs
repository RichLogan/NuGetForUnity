﻿namespace NugetForUnity
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Represents the NuGet Package Manager Window in the Unity Editor.
    /// </summary>
    public class NugetWindow : EditorWindow
    {
        /// <summary>
        /// The current position of the scroll bar in the GUI.
        /// </summary>
        private Vector2 scrollPosition;

        /// <summary>
        /// The list of NugetPackages available to install.
        /// </summary>
        private List<NugetPackage> packages;

        /// <summary>
        /// The list of NugetPackages already installed.
        /// </summary>
        private List<NugetPackage> installedPackages;

        /// <summary>
        /// True to show all old package versions.  False to only show the latest version.
        /// </summary>
        private bool showAllVersions;

        /// <summary>
        /// True to show beta and alpha package versions.  False to only show stable versions.
        /// </summary>
        private bool showPrerelease;

        /// <summary>
        /// The width to use for the install/uninstall/update/downgrade button
        /// </summary>
        private readonly GUILayoutOption installButtonWidth = GUILayout.Width(160);

        /// <summary>
        /// The search term to search the packages for.
        /// </summary>
        private string searchTerm = "Search";

        /// <summary>
        /// The number of packages to get from the request to the server.
        /// </summary>
        private int numberToGet = 15;

        /// <summary>
        /// The number of packages to skip when requesting a list of packages from the server.  This is used to get a new group of packages.
        /// </summary>
        private int numberToSkip;

        /// <summary>
        /// Opens the NuGet Package Manager Window.
        /// </summary>
        [MenuItem("NuGet/Manage NuGet Packages")]
        protected static void DisplayNugetWindow()
        {
            GetWindow<NugetWindow>();
        }

        /// <summary>
        /// Restores all packages defined in packages.config
        /// </summary>
        [MenuItem("NuGet/Restore Packages")]
        protected static void RestorePackages()
        {
            NugetHelper.RestoreHttp();
        }

        /// <summary>
        /// Called when enabling the window.
        /// </summary>
        private void OnEnable()
        {
            // set the window title
            titleContent = new GUIContent("NuGet");

            // reset the number to skip
            numberToSkip = 0;

            // update the packages list
            UpdatePackages();

            // load a list of install packages
            installedPackages = NugetHelper.LoadInstalledPackages();
        }

        /// <summary>
        /// Updates the list of available packages by running a search with the server using the currently set parameters (# get, # skip, etc).
        /// </summary>
        private void UpdatePackages()
        {
            packages = NugetHelper.Search(searchTerm != "Search" ? searchTerm : string.Empty, showAllVersions, showPrerelease, numberToGet, numberToSkip);
        }

        /// <summary>
        /// From here: http://forum.unity3d.com/threads/changing-the-background-color-for-beginhorizontal.66015/
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }

        /// <summary>
        /// Draws the GUI.
        /// </summary>
        protected void OnGUI()
        {
            GUIStyle headerStyle = new GUIStyle();
            headerStyle.normal.background = MakeTex(20, 20, new Color(0.05f, 0.05f, 0.05f));

            // dislay the header
            EditorGUILayout.BeginVertical(headerStyle);
            {
                EditorGUILayout.BeginHorizontal();
                {
                    bool showAllVersionsTemp = EditorGUILayout.Toggle("Show All Versions", showAllVersions);
                    if (showAllVersionsTemp != showAllVersions)
                    {
                        showAllVersions = showAllVersionsTemp;
                        UpdatePackages();
                    }

                    if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                    {
                        OnEnable();
                    }
                }
                EditorGUILayout.EndHorizontal();

                bool showPrereleaseTemp = EditorGUILayout.Toggle("Show Prerelease", showPrerelease);
                if (showPrereleaseTemp != showPrerelease)
                {
                    showPrerelease = showPrereleaseTemp;
                    UpdatePackages();
                }

                // search if the search term was changed
                string searchTermTemp = EditorGUILayout.TextField(searchTerm);
                if (searchTermTemp != searchTerm)
                {
                    searchTerm = searchTermTemp;

                    // reset the number to skip
                    numberToSkip = 0;
                    UpdatePackages();
                }
            }
            EditorGUILayout.EndVertical();

            // display all of the packages
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.BeginVertical();

            GUIStyle style = new GUIStyle();
            style.normal.background = MakeTex(20, 20, new Color(0.3f, 0.3f, 0.3f));

            if (packages != null)
            {
                for (int i = 0; i < packages.Count; i++)
                {
                    if (i%2 == 0)
                        EditorGUILayout.BeginVertical();
                    else
                        EditorGUILayout.BeginVertical(style);

                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorStyles.label.fontStyle = FontStyle.Bold;
                        EditorStyles.label.fontSize = 14;
                        EditorGUILayout.LabelField(string.Format("{1} [{0}]", packages[i].Version, packages[i].Id), GUILayout.Height(20));
                        EditorStyles.label.fontSize = 10;

                        if (installedPackages.Contains(packages[i]))
                        {
                            // This specific version is installed
                            if (GUILayout.Button("Uninstall", installButtonWidth))
                            {
                                installedPackages.Remove(packages[i]);
                                NugetHelper.Uninstall(packages[i]);
                            }
                        }
                        else
                        {
                            var installed = installedPackages.FirstOrDefault(p => p.Id == packages[i].Id);
                            if (installed != null)
                            {
                                if (CompareVersions(installed.Version, packages[i].Version) < 0)
                                {
                                    // An older version is installed
                                    if (GUILayout.Button(string.Format("Update [{0}]", installed.Version), installButtonWidth))
                                    {
                                        NugetHelper.Update(installed, packages[i]);
                                        installedPackages = NugetHelper.LoadInstalledPackages();
                                    }
                                }
                                else if (CompareVersions(installed.Version, packages[i].Version) > 0)
                                {
                                    // A newer version is installed
                                    if (GUILayout.Button(string.Format("Downgrade [{0}]", installed.Version), installButtonWidth))
                                    {
                                        NugetHelper.Update(installed, packages[i]);
                                        installedPackages = NugetHelper.LoadInstalledPackages();
                                    }
                                }
                            }
                            else
                            {
                                if (GUILayout.Button("Install", installButtonWidth))
                                {
                                    NugetHelper.InstallHttp(packages[i]);
                                    AssetDatabase.Refresh();
                                    installedPackages = NugetHelper.LoadInstalledPackages();
                                }
                            }
                            
                        }
                        
                    }
                    EditorGUILayout.EndHorizontal();

                    // Show the package description
                    EditorStyles.label.wordWrap = true;
                    EditorStyles.label.fontStyle = FontStyle.Normal;
                    EditorGUILayout.LabelField(string.Format("{0}", packages[i].Description));

                    // Show the license button
                    if (GUILayout.Button("View License", GUILayout.Width(120)))
                    {
                        Application.OpenURL(packages[i].LicenseUrl);
                    }

                    EditorGUILayout.Separator();
                    EditorGUILayout.Separator();

                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();

            // allow the user to dislay more results
            if (GUILayout.Button("Show More", GUILayout.Width(120)))
            {
                numberToSkip += numberToGet;
                packages.AddRange(NugetHelper.Search(searchTerm != "Search" ? searchTerm : string.Empty, showAllVersions, showPrerelease, numberToGet, numberToSkip));
            }
        }

        /// <summary>
        /// Compares two version numbers in the form "1.2.1". Returns:
        /// -1 if versionA is less than versionB
        ///  0 if versionA is equal to versionB
        /// +1 if versionA is greater than versionB
        /// </summary>
        /// <param name="versionA">The first version number to compare.</param>
        /// <param name="versionB">The second version number to compare.</param>
        /// <returns>-1 if versionA is less than versionB. 0 if versionA is equal to versionB. +1 if versionA is greater than versionB</returns>
        private int CompareVersions(string versionA, string versionB)
        {
            try
            {
                // TODO: Compare the prerelease beta/alpha tag
                versionA = versionA.Split('-')[0];
                string[] splitA = versionA.Split('.');
                int majorA = int.Parse(splitA[0]);
                int minorA = int.Parse(splitA[1]);
                int patchA = int.Parse(splitA[2]);
                int buildA = 0;
                if (splitA.Length == 4)
                {
                    buildA = int.Parse(splitA[3]);
                }

                versionB = versionB.Split('-')[0];
                string[] splitB = versionB.Split('.');
                int majorB = int.Parse(splitB[0]);
                int minorB = int.Parse(splitB[1]);
                int patchB = int.Parse(splitB[2]);
                int buildB= 0;
                if (splitB.Length == 4)
                {
                    buildB = int.Parse(splitB[3]);
                }

                int major = majorA < majorB ? -1 : majorA > majorB ? 1 : 0;
                int minor = minorA < minorB ? -1 : minorA > minorB ? 1 : 0;
                int patch = patchA < patchB ? -1 : patchA > patchB ? 1 : 0;
                int build = buildA < buildB ? -1 : buildA > buildB ? 1 : 0;

                if (major == 0)
                {
                    // if major versions are equal, compare minor versions
                    if (minor == 0)
                    {
                        if (patch == 0)
                        {
                            // if patch versions are equal, just use the build version
                            return build;
                        }

                        // the patch versions are different, so use them
                        return patch;
                    }

                    // the minor versions are different, so use them
                    return minor;
                }

                // the major versions are different, so use them
                return major;
            }
            catch (Exception)
            {
                Debug.LogErrorFormat("Compare Error: {0} {1}", versionA, versionB);
                return 0;
            }
        }
    }
}