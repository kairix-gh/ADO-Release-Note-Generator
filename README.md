## ADO Release Notes Gnerator
This is a simple tool which when ran will generate a PDF with release notes based on items found in AzureDevOps. Configuration can be managed by using the appsettings.json file.

#### Getting Started
First you will need a personal access token to Azure DevOps. Instructions to create one can be found [here](https://learn.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops&tabs=Windows). Please ensure your personal access token has the permissions to read work items.

Once you have a personal access token you will need to add it to the appsettings.json configuration file under the AzureDevOps object. While here, please be sure to fill out the following items:
 - Azure DevOps URL
 - Release Version & Date
 - Confirm the address, hyperlink, and hyperlink text configuration items are correct.
 
 Next you will need to configure the work items to retreive from Azure DevOps to build the release notes document.

#### Configuring Work Items
The appsettings.json file contains an object array titled 'WorkItemGroups'. Each object contains a Name, Query, Fields, TitleField, and DescriptionField, which are explained in the table below:

| Key | Description |
| --- | --- |
| Name | The name of the work item group. Items of the same group will appear under a header using this name in the release notes document. |
| Query | The WIQL query that is ran to retreive the work items from Azure DevOps. |
| Fields | A comma separated list of fields to retreive from Azure DevOps for this group of WorkItems. |
| TitleField | The field to be used as the title of the release note. |
| DescriptionField | The field to be used as the description of the release note. |

This tool will run the specified query for each work item group, retrieve the appropriate fields, and build release notes for that group using the name and supplied fields for the title and description of each item. You can use any valid WIQL query to target items that are appropriate for the release. For more information on the WIQL syntax, you can find some [documentation here](https://learn.microsoft.com/en-us/azure/devops/boards/queries/wiql-syntax?view=azure-devops).

#### Running the Application
It is recommended you run the application from the command line so you can see the console output; however, you are able to run executable directly and the output will still be the same. If everything was configured correctly, you will see the generated release notes document, in PDF format, get created in the same directory as the executable.  

#### Command Line Arguments
This tool supports a few command line arguments to make it easier to integrate into an automated environment, any value set via cli arguments will override the configuration in the appsettings.json file.

| Command | Effect |
| --- | --- |
| -r | Sets the release version. Example: ``` -r 10 ``` |
| -d | Sets the release date, for en-US please use m/d/yyyy format. Example: ``` -d 3/5/2023 ``` |
| -o | Sets the output directory of the generated release notes document. **Please note this should only be a path, do not include the file name.**  Example: ``` -o C:\users\kairix\desktop\ ``` |
| -l | Sets a directory/filename to save logs to. Example: ``` -l C:\logs\log.txt ``` |

#### Configuration Details

| Configuration Key | Description |
| --- | --- |
| FooterAddress | Sets the address used in the footer of the coverage for the PDF document. |
| FooterHyperlink | Sets the URL users are directed to when clicking on the footer image or link. |
| FooterHyperlinkText | The visible text users see and can click on to take them to ```FooterHyperlink```. |
| FooterImagePath | Relaive path to the image used in the footer on the cover page. |
| HeaderImagePath | Relative path to the iamge used in the header on pages 2+ |
| SkipWorkItemsWithNoNotes | When true, any work items that do not have a description set will be skipped and not included in the release notes. The description is determined by the ```DescriptionField``` in the WorkItemGroups. |
| ReleaseInfo:Version | This is the version the the release notes correspond to. |
| ReleaseInfo:Date | This is the date of the release |
| AzureDevOps:Url | This is your *organization* URL for AzureDevOps |
| AzureDevOps:Token | This is your personal access token to Azure DevOps. Please do not share this and consider it in the same regard as a password. |
| WorkItemGroups | This is an array of objects, each of which represent a grouping of work items in the release notes document. There is no limit to the number of groupings you can have. |
| WorkItemGroups:Name | The name of the work item group. Items of the same group will appear under a header using this name in the release notes document. |
| WorkItemGroups:Query | The WIQL query that is ran to retreive the work items from Azure DevOps. |
| WorkItemGroups:Fields | A comma separated list of fields to retreive from Azure DevOps for this group of WorkItems. |
| WorkItemGroups:TitleField | The field to be used as the title of the release note. |
| WorkItemGroups:DescriptionField | The field to be used as the description of the release note. |
