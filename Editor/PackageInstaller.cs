using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CJM.BarracudaInference.YOLOX
{
    // Serializable class to hold package data
    [System.Serializable]
    public class PackageData
    {
        public string packageName;
        public string packageUrl;
    }

    // Serializable class to hold a list of PackageData objects
    [System.Serializable]
    public class PackageList
    {
        public List<PackageData> packages;
    }

    public class PackageInstaller
    {
        // Stores the AddRequest object for the current package to install.
        private static AddRequest addRequest;
        // A list of PackageData objects to install.
        private static List<PackageData> packagesToInstall;
        // The index of the current package to install.
        private static int currentPackageIndex;

        // GUID of the JSON file containing the list of packages to install
        private const string PackagesJSONGUID = "02aec9cd479b4b758a7afde0032230ec";

        // Method called on load to install packages from the JSON file
        [InitializeOnLoadMethod]
        public static void InstallDependencies()
        {
            // Read the package JSON file
            packagesToInstall = ReadPackageJson().packages;
            // Initialize the current package index
            currentPackageIndex = 0;
            // Start installing the packages
            InstallNextPackage();
        }

        // Method to install the next package in the list
        private static void InstallNextPackage()
        {
            // Iterate through package list
            if (currentPackageIndex < packagesToInstall.Count)
            {
                PackageData packageData = packagesToInstall[currentPackageIndex];

                // Check if the package is already installed
                if (!IsPackageInstalled(packageData.packageName))
                {
                    // Attempt to install package
                    addRequest = Client.Add(packageData.packageUrl);
                    EditorApplication.update += PackageInstallationProgress;
                }
                else
                {
                    // Increment the current package index
                    currentPackageIndex++;
                    // Recursively call InstallNextPackage
                    InstallNextPackage();
                }
            }
        }

        // Method to monitor the progress of package installation
        private static void PackageInstallationProgress()
        {
            if (addRequest.IsCompleted)
            {
                // Log whether the package installation was successful
                if (addRequest.Status == StatusCode.Success)
                {
                    UnityEngine.Debug.Log($"Successfully installed: {addRequest.Result.packageId}");
                }
                else if (addRequest.Status >= StatusCode.Failure)
                {
                    UnityEngine.Debug.LogError($"Failed to install package: {addRequest.Error.message}");
                }

                // Unregister the method from the EditorApplication.update 
                EditorApplication.update -= PackageInstallationProgress;
                // Increment the current package index
                currentPackageIndex++;
                // Install the next package in the list
                InstallNextPackage();
            }
        }

        // Method to check if a package is already installed
        private static bool IsPackageInstalled(string packageName)
        {
            // List the installed packages
            var listRequest = Client.List(true, false);
            while (!listRequest.IsCompleted) { }

            if (listRequest.Status == StatusCode.Success)
            {
                // Check if the package is already installed
                return listRequest.Result.Any(package => package.name == packageName);
            }
            else
            {
                UnityEngine.Debug.LogError($"Failed to list packages: {listRequest.Error.message}");
            }

            return false;
        }

        // Method to read the JSON file and return a PackageList object
        private static PackageList ReadPackageJson()
        {
            // Convert the PackagesJSONGUID to an asset path
            string assetPath = AssetDatabase.GUIDToAssetPath(PackagesJSONGUID);
            // Read the JSON file content as a string
            string jsonString = File.ReadAllText(assetPath);
            // Deserialize the JSON string into a PackageList object
            return JsonUtility.FromJson<PackageList>(jsonString);
        }


    }
}
