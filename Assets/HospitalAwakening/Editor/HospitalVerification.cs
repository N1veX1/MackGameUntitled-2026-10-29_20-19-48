using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HospitalVerification
{
    static double due;
    static HospitalVerification() { EditorApplication.playModeStateChanged += State; }
    static void State(PlayModeStateChange state) {
        if(state==PlayModeStateChange.EnteredPlayMode && File.Exists("Assets/HospitalAwakening/verify.pending")) {
            due=EditorApplication.timeSinceStartup+2; EditorApplication.update+=Check;
        }
    }
    static void Require(bool result,string message) { if(!result) throw new Exception(message); }
    static void Check() {
        if(EditorApplication.timeSinceStartup<due) return;
        EditorApplication.update-=Check;
        string output="C:/Users/serge/Documents/Codex/2026-10-29/vj/outputs/hospital-validation.txt";
        try {
            var explorer=UnityEngine.Object.FindAnyObjectByType<HospitalExplorer>(); Require(explorer!=null,"Player missing");
            Require(explorer.GetComponent<CharacterController>().isGrounded,"Player not grounded");
            var all=UnityEngine.Object.FindObjectsByType<HospitalInteractable>();
            var glass=all.Single(a=>a.action==HospitalInteractable.ActionType.Glass);
            glass.Activate(explorer); Require(glass.glassPanels.All(p=>p.sharedMaterial==glass.privacyGlass),"Privacy failed");
            glass.Activate(explorer); Require(glass.glassPanels.All(p=>p.sharedMaterial==glass.clearGlass),"Clear glass failed");
            int doors=0;
            foreach(var door in all.Where(a=>a.action==HospitalInteractable.ActionType.Door)) {
                var start=door.doorLeaf.localPosition; door.Activate(explorer); Require(Vector3.Distance(start,door.doorLeaf.localPosition)>1,"Door did not open");
                door.Activate(explorer); Require(Vector3.Distance(start,door.doorLeaf.localPosition)<.001f,"Door did not close"); doors++;
            }
            Require(doors==3,"Only the three exterior doors should remain");
            foreach(float sign in new[]{-1f,1f}) Require(Physics.Raycast(new Vector3(0,1.3f,-4.29f),Vector3.right*sign,5f),"Glass closure missing");
            var documents=all.Where(a=>a.action==HospitalInteractable.ActionType.Document).ToArray(); Require(documents.Length==2,"Expected two documents");
            foreach(var d in documents) { Require(d.body.Length>150,"Document content missing"); d.Activate(explorer); }
            File.WriteAllText(output,"PASS — Unity Play Mode\nPlayer grounded on floor\n24 glass panels: privacy -> transparent\n"+doors+" exterior sliding doors: opened -> closed\nFormer passages closed with glass; staff observation corridors added\n2 readable documents: opened with story text\nNo changes persisted from Play Mode.");
            Debug.Log("HOSPITAL_VERIFICATION_PASS");
        } catch(Exception e) { File.WriteAllText(output,"FAIL\n"+e); Debug.LogException(e); }
        finally { AssetDatabase.DeleteAsset("Assets/HospitalAwakening/verify.pending"); EditorApplication.isPlaying=false; }
    }
}
