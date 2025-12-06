//Create a new Dropdown GameObject by going to the Hierarchy and clicking Create>UI>Dropdown. Attach this script to the Dropdown GameObject.
//Set your own Text in the Inspector window

using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class DropDownHandler : MonoBehaviour
{
    public TMP_Dropdown myDropdown; 

    void Start()
    {
        //Fetch the Dropdown GameObject
        myDropdown = GetComponent<TMP_Dropdown>();
        //Add listener for when the value of the Dropdown changes, to take action
        myDropdown.onValueChanged.AddListener(delegate {
            DropdownValueChanged(myDropdown);
        });
    }

    //Ouput the new value of the Dropdown into Text
    void DropdownValueChanged(TMP_Dropdown change)
    {
        // Get the index of the selected option
        int selectedIndex = myDropdown.value;

        // Get the text of the selected option
        string selectedOptionText = myDropdown.options[selectedIndex].text;

        //Initialise the Text to say the first value of the Dropdown
        //Debug.Log("dropdown : " + selectedOptionText);
        Actions.RespondToShape(selectedOptionText);
    }
}