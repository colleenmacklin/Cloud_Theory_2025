using UnityEngine;
using TMPro;
using UnityEngine.UI;

public enum DropdownType
{
    Theme,
    Cloud,
    Voice
}
public class UIHandler : MonoBehaviour
{
    public void HandleDropdownChange(TMP_Dropdown changedDropdown)
    {
        // Get the identifier script attached to the same GameObject
        Identifier dropDownID = changedDropdown.GetComponent<Identifier>();

        if (dropDownID != null)
        {
            DropdownType type = dropDownID.myDropDownType;
            int value = changedDropdown.value;
            string selectedText = changedDropdown.options[value].text;
            //Debug.Log($"Dropdown '{id}' changed to index {value} (Text: {selectedText})");

            switch (type)
            {
                case DropdownType.Theme: 
                Actions.ChooseTheme(selectedText);
                break;
                
                case DropdownType.Cloud:
                //dropDown
                Actions.ChooseCloud(selectedText);
                break;
                
                case DropdownType.Voice:
                //dropDown
                Actions.ChooseVoice(selectedText);
                break;

                
                default:
                Debug.Log("no dropdown type identified");
                break;

            }
        }
    }


}
