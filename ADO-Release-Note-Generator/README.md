## ADO Release Notes Gnerator
This is a simple tool which when ran will generate a PDF with release notes based on items found in AzureDevOps. Configuration can be managed by using the appsettings.json file.

#### Configuring Work Items
The appsettings.json file contains an object array titled 'WorkItemGroups'. Each object contains a Name, Query, Fields, TitleField, and DescriptionField. 

| Key | Description |
| Name | The name of the work item group. Items of the same group will appear under a header using this name in the release notes document. |
| Query | The WIQL query that is ran to retreive the work items from Azure DevOps. |
| Fields | A comma separated list of fields to retreive from Azure DevOps for this group of WorkItems. |
| TitleField | The field to be used as the title of the release note. |
| DescriptionField | The field to be used as the description of the release note. |

For each object in the array the application will run the WIQL query in the object to retreive the specified fields. They will then be grouped together on the release notes under the provided name, using the specified fields for the title and description for each release note.
