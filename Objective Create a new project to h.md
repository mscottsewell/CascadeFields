Objective: Create a new project to host an XrmToolBox utility "CascadeFields Configurator" that provides a GUI for the user to configure and deploy the CascadeFields plugin.  
use the Plugin base class found here: https://www.xrmtoolbox.com/documentation/for-developers/plugincontrolbase-base-class/



### Definitions

\*\* Parent entity \*\* - This is the entity that we're watching for changes on and when changes are made, we copy values from this record to the child record.

\*\* Child entity \*\* - This is an entity that has a lookup relationship to the Parent entity.  This entity will be updated with values/changes from the parent record.



## Form Layout

* The form is divided into two panes with a button ribbon across the top.
* There are 6 combo boxes on the left-hand pane of the form.
* In the right pane are two tabs, "Field Mappings" and "JSON Preview"
* There is a ribbon of buttons with monoline-style (blank and white) icons and labels for each selection.



### Ribbon Layout

Across the ribbon these buttons are used by the user to interact with the plugin.

1. Load Metadata - Refresh the metadata from Dataverse needed to populate the combo boxes.
2. Retrieve Configured Entity - Query the associated plugin's deployed steps to retrieve any one configured Parent-Child mapped relationships. When selected display a popup window with a list of configured parent-child relationships and allow the user to select one or cancel. - If one is selected, retrieve it and clear and re-populate the form with that relationship's configuration and mapping.
3. Update Cascade Fields Plug-in - Deploy or Update the plugin registered in Dataverse with the latest / current version of the plugin from CascadeFields.Plugin
4. Publish Configuration - Publish the configured mappings from the utility to Dataverse and save the session settings.
   Publishing the configuration will complete all steps needed to create the plugin steps, Preimage and all the rest of things needed to successfully deploy the configuration JSON from this utility to the dataverse plugin registration. Consult with the CascadeFields.Plugin documentation to determine what is needed.



### Left-hand Pane

The 5 combo boxes and a log window on the left-pane:

If there are saved session settings for the current connection, these will be used as the default values for the combo boxes.

As selections are made, update the saved session settings.



###### Solution

* Show the display names of all unmanaged solutions in the connected environment.
* Keep the ID of the unmanaged solutions as the identifier.
* If a saved solution from the prior session is available, use that as the currently selected solution.
* If a saved solution is not available, default to the 'Default' solution.



###### Parent Entity

* Filter this to the entities listed in the Solution.
* Show the Display name with the schema name in parenthesis in the drop-down list
* If the Solution is changed, and this entity is not in the newly selected solution, clear it and all values below it.



###### Parent Form (optional)

* A combo box containing All forms (type = main) in the selected Solution for the selected Parent Entity.
* Show the Form Name for selection, but keep the ID of the form available
* If the Solution or Parent Entity is changed, clear this list and all values below it.



###### Child Entity

* Filter this to the editable entities listed in the solution that are connected to the selected Parent Entity via a Many(child)-to-one(parent) relationship.
* (There's no dependency on the Parent Form for this list.)
* Show the related Entity and the lookup field from that entity in parenthesis in the drop-down list. (This is important since there can be more than one field on the child related to the same parent, but this configuration is tied to a specific relationship.)
* When selected, if there is an existing mapping for this parent-child relationship already configured, load the mapping table with the existing configuration.
* If the Solution or Parent entity is changed, clear this field and all values below it.



###### Child Form (optional)

* A combo box with all forms (type = main) in the selected Solution for the selected Child Entity.
* Show the Form Name for selection, but keep the ID of the form available
* If the Child Entity is changed, clear this list and all values below it.



###### Log

* Below the Combo boxes is a window displaying a log of the events steps firing in the loading/configuration of the app.
* The Log text not editable, but it can be highlighted to copy/paste elsewhere for troubleshooting / analysis.





### Right-hand Pane:



#### Mapping Tab (default):

* The default tab is the Mapping grid where the user will add/remove/configure the mapped field pairs between the parent and child.
* If no mapping exists, show a blank line waiting to be configured.
* When one mapping line is configured, add a blank line under it waiting to be configured.



##### The first column is the "Source: Parent" field selection combo box.

* Filter this combo box to the fields in the Parent Entity.
* If a Parent Entity Form is selected, filter the selection list to only the fields from the parent entity that are on that form.



##### The second Column is the "Destination: Child" field selection combo box.

* This column is enabled after the first column is selected.
* Filter this combo box to the fields from the Child entity that are compatible with receiving data from the first column.
* Compatibility is defined in the plug-in documentation.







#### JSON Preview Tab:

* This is a read-only window of the JSON currently being configured.
* It's not editable, but it can be highlighted to copy/paste elsewhere
