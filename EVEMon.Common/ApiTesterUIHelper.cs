using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace EVEMon.Common
{
    public static class ApiTesterUIHelper
    {
        #region Fields

        private const string NoAPIKeyWithAccess = "No API key with access to API call found";

        private static readonly Enum[] s_apiMethodsHasIDOrName = new Enum[]
                                                                     {
                                                                         APIGenericMethods.CharacterID,
                                                                         APIGenericMethods.CharacterName,
                                                                         APIGenericMethods.TypeName,
                                                                         APIGenericMethods.ContractItems,
                                                                         APIGenericMethods.CorporationContractItems,
                                                                         APICharacterMethods.CalendarEventAttendees,
                                                                         APICharacterMethods.Locations,
                                                                         APICharacterMethods.MailBodies,
                                                                         APICharacterMethods.NotificationTexts,
                                                                         APICorporationMethods.CorporationLocations,
                                                                         APICorporationMethods.CorporationStarbaseDetails
                                                                     };

        #endregion


        #region Properties

        /// <summary>
        /// Sets a value indicating whether we use internal info.
        /// </summary>
        /// <value>
        ///   <c>true</c> if we use internal info; otherwise, <c>false</c>.
        /// </value>
        public static bool UseInternalInfo { get; set; }

        /// <summary>
        /// Sets a value indicating whether we use external info.
        /// </summary>
        /// <value>
        ///   <c>true</c> if we use external info; otherwise, <c>false</c>.
        /// </value>
        public static bool UseExternalInfo { get; set; }

        /// <summary>
        /// Sets the selected item.
        /// </summary>
        /// <value>
        /// The selected item.
        /// </value>
        public static object SelectedItem { get; set; }

        /// <summary>
        /// Sets the selected character.
        /// </summary>
        /// <value>
        /// The selected character.
        /// </value>
        public static object SelectedCharacter { get; set; }

        /// <summary>
        /// Sets the key ID.
        /// </summary>
        /// <value>
        /// The key ID.
        /// </value>
        public static string KeyID { get; set; }

        /// <summary>
        /// Sets the Verification code.
        /// </summary>
        /// <value>
        /// The Verification code.
        /// </value>
        public static string VCode { get; set; }

        /// <summary>
        /// Sets the char ID.
        /// </summary>
        /// <value>
        /// The char ID.
        /// </value>
        public static string CharID { get; set; }

        /// <summary>
        /// Sets the ID or name text.
        /// </summary>
        /// <value>
        /// The ID or name text.
        /// </value>
        public static string IDOrNameText { get; set; }

        /// <summary>
        /// Gets the error text.
        /// </summary>
        public static string ErrorText { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the API method has ID or name parameter.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if the API method has ID or name parameter; otherwise, <c>false</c>.
        /// </value>
        public static bool HasIDOrName
        {
            get { return SelectedItem != null && s_apiMethodsHasIDOrName.Any(method => SelectedItem.Equals(method)); }
        }

        /// <summary>
        /// Gets the URL.
        /// </summary>
        /// <value>The URL.</value>
        public static Uri Url
        {
            get
            {
                string postData = GetPostData();
                string uriString = EveMonClient.APIProviders.CurrentProvider.GetMethodUrl((Enum)SelectedItem).AbsoluteUri;

                ErrorText = (postData == NoAPIKeyWithAccess ? NoAPIKeyWithAccess : String.Empty);

                if (!String.IsNullOrWhiteSpace(postData) && postData != NoAPIKeyWithAccess)
                    uriString += String.Format(CultureConstants.InvariantCulture, "?{0}", postData);

                return new Uri(uriString);
            }
        }

        /// <summary>
        /// Gets the API methods.
        /// </summary>
        public static IEnumerable<Enum> GetApiMethods
        {
            get
            {
                // List the API methods by type and name
                // Add the Server Status method on top
                List<Enum> apiMethods = new List<Enum> { APIGenericMethods.ServerStatus };

                // Add the non Account type methods
                apiMethods.AddRange(APIMethods.Methods.OfType<APIGenericMethods>().Where(
                    method => !apiMethods.Contains(method) &&
                              APIMethods.NonAccountGenericMethods.Contains(method)).Cast<Enum>().OrderBy(
                                  method => method.ToString()));

                // Add the Account type methods
                apiMethods.AddRange(APIMethods.Methods.OfType<APIGenericMethods>().Where(
                    method => !apiMethods.Contains(method) && !APIMethods.NonAccountGenericMethods.Contains(method) &&
                              !APIMethods.AllSupplementalMethods.Contains(method)).Cast<Enum>().OrderBy(
                                  method => method.ToString()));

                // Add the character methods
                apiMethods.AddRange(
                    APIMethods.Methods.OfType<APICharacterMethods>().Cast<Enum>().Concat(
                        APIMethods.CharacterSupplementalMethods).OrderBy(method => method.ToString()));

                // Add the corporation methods
                apiMethods.AddRange(
                    APIMethods.Methods.OfType<APICorporationMethods>().Cast<Enum>().Concat(
                        APIMethods.CorporationSupplementalMethods).OrderBy(method => method.ToString()));

                return apiMethods;
            }
        }

        /// <summary>
        /// Gets the characters.
        /// </summary>
        public static IEnumerable<CCPCharacter> GetCharacters
        {
            get
            {
                return EveMonClient.Characters.OfType<CCPCharacter>().Where(
                    character => character.Identity.APIKeys.Any()).OrderBy(character => character.Name);
            }
        }

        #endregion


        #region Private Helper Methods

        /// <summary>
        /// Gets the post data.
        /// </summary>
        /// <returns></returns>
        private static string GetPostData()
        {
            if (SelectedItem is APIGenericMethods)
                return GetPostDataForGenericAPIMethods();

            if (SelectedItem is APICharacterMethods)
                return GetCharacterAPIMethodsPostData();

            if (SelectedItem is APICorporationMethods)
                return GetCorporationAPIMethodsPostData();

            return String.Empty;
        }

        /// <summary>
        /// Gets the post data for generic API methods.
        /// </summary>
        /// <returns></returns>
        private static string GetPostDataForGenericAPIMethods()
        {
            if (SelectedItem == null)
                return String.Empty;

            if (SelectedItem.Equals(APIGenericMethods.CharacterName) ||
                SelectedItem.Equals(APIGenericMethods.TypeName))
            {
                return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataIDsOnly, IDOrNameText);
            }

            if (SelectedItem.Equals(APIGenericMethods.CharacterID))
                return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataNamesOnly, IDOrNameText);

            if (APIMethods.AllSupplementalMethods.Contains(SelectedItem))
                return SupplementalAPIMethodsPostData();

            if (!APIMethods.NonAccountGenericMethods.Contains(SelectedItem))
            {
                if (UseInternalInfo)
                {
                    if (SelectedCharacter == null)
                        return String.Empty;

                    Character character = (Character)SelectedCharacter;
                    APIKey apiKey = character.Identity.APIKeys.FirstOrDefault(key => key.IsCharacterOrAccountType);

                    return apiKey == null
                               ? NoAPIKeyWithAccess
                               : String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataBase,
                                               apiKey.ID, apiKey.VerificationCode);
                }

                if (UseExternalInfo)
                    return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataBase,
                                         KeyID, VCode);
            }

            return String.Empty;
        }

        /// <summary>
        /// Gets the post data for the supplemental API methods.
        /// </summary>
        /// <returns></returns>
        private static string SupplementalAPIMethodsPostData()
        {
            if (SelectedItem == null)
                return String.Empty;

            if (UseInternalInfo)
            {
                if (SelectedCharacter == null)
                    return String.Empty;

                Character character = (Character)SelectedCharacter;
                APIKey apiKey = null;

                if (SelectedItem.ToString().StartsWith("CorporationContract", StringComparison.Ordinal))
                    apiKey = character.Identity.FindAPIKeyWithAccess(APICorporationMethods.CorporationContracts);

                if (SelectedItem.ToString().StartsWith("Contract", StringComparison.Ordinal))
                    apiKey = character.Identity.FindAPIKeyWithAccess(APICharacterMethods.Contracts);

                if (apiKey == null)
                    return NoAPIKeyWithAccess;

                if (SelectedItem.Equals(APIGenericMethods.ContractItems) ||
                    SelectedItem.Equals(APIGenericMethods.CorporationContractItems))
                {
                    return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataWithCharIDAndContractID,
                                         apiKey.ID, apiKey.VerificationCode, character.CharacterID, IDOrNameText);
                }

                return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataWithCharID,
                                     apiKey.ID, apiKey.VerificationCode, character.CharacterID);
            }

            if (SelectedItem.Equals(APIGenericMethods.ContractItems) ||
                SelectedItem.Equals(APIGenericMethods.CorporationContractItems))
            {
                return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataWithCharIDAndContractID,
                                     KeyID, VCode, CharID, IDOrNameText);
            }

            return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataWithCharID,
                                 KeyID, VCode, CharID);
        }

        /// <summary>
        /// Gets the post data for character API methods.
        /// </summary>
        /// <returns></returns>
        private static string GetCharacterAPIMethodsPostData()
        {
            if (SelectedItem == null)
                return String.Empty;

            if (UseInternalInfo)
            {
                if (SelectedCharacter == null)
                    return String.Empty;

                Character character = (Character)SelectedCharacter;
                APIKey apiKey = character.Identity.FindAPIKeyWithAccess((APICharacterMethods)SelectedItem);

                if (apiKey == null)
                    return NoAPIKeyWithAccess;

                if (SelectedItem.Equals(APICharacterMethods.CalendarEventAttendees) ||
                    SelectedItem.Equals(APICharacterMethods.Locations) ||
                    SelectedItem.Equals(APICharacterMethods.MailBodies) ||
                    SelectedItem.Equals(APICharacterMethods.NotificationTexts))
                {
                    return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataWithCharIDAndIDS,
                                         apiKey.ID, apiKey.VerificationCode, character.CharacterID, IDOrNameText);
                }

                return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataWithCharID,
                                     apiKey.ID, apiKey.VerificationCode, character.CharacterID);
            }

            if (SelectedItem.Equals(APICharacterMethods.CharacterInfo) &&
                (KeyID.Length == 0 || VCode.Length == 0))
            {
                return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataCharacterIDOnly,
                                     CharID);
            }

            if (SelectedItem.Equals(APICharacterMethods.Locations) ||
                SelectedItem.Equals(APICharacterMethods.MailBodies) ||
                SelectedItem.Equals(APICharacterMethods.NotificationTexts))
            {
                return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataWithCharIDAndIDS,
                                     KeyID, VCode, CharID, IDOrNameText);
            }

            return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataWithCharID,
                                 KeyID, VCode, CharID);
        }

        /// <summary>
        /// Gets the post data for corporation API methods.
        /// </summary>
        /// <returns></returns>
        private static string GetCorporationAPIMethodsPostData()
        {
            if (SelectedItem == null)
                return String.Empty;

            if (UseInternalInfo)
            {
                if (SelectedCharacter == null)
                    return String.Empty;

                Character character = (Character)SelectedCharacter;
                APIKey apiKey = character.Identity.FindAPIKeyWithAccess((APICorporationMethods)SelectedItem);

                if (apiKey == null)
                    return NoAPIKeyWithAccess;

                if (SelectedItem.Equals(APICorporationMethods.CorporationLocations))
                {
                    return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataWithCharIDAndIDS,
                                         apiKey.ID, apiKey.VerificationCode, character.CharacterID, IDOrNameText);
                }

                if (SelectedItem.Equals(APICorporationMethods.CorporationMemberTrackingExtended))
                {
                    return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataWithExtendedParameter,
                                         apiKey.ID, apiKey.VerificationCode);
                }

                if (SelectedItem.Equals(APICorporationMethods.CorporationStarbaseDetails))
                {
                    return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataWithItemID,
                                         apiKey.ID, apiKey.VerificationCode, IDOrNameText);
                }

                return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataBase,
                                     apiKey.ID, apiKey.VerificationCode);
            }

            if (SelectedItem.Equals(APICorporationMethods.CorporationSheet) &&
                (KeyID.Length == 0 || VCode.Length == 0))
            {
                return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataCorporationIDOnly,
                                     CharID);
            }

            if (SelectedItem.Equals(APICorporationMethods.CorporationLocations))
            {
                return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataWithCharIDAndIDS,
                                     KeyID, VCode, CharID, IDOrNameText);
            }

            if (SelectedItem.Equals(APICorporationMethods.CorporationMemberTrackingExtended))
            {
                return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataWithExtendedParameter,
                                     KeyID, VCode);
            }

            if (SelectedItem.Equals(APICorporationMethods.CorporationStarbaseDetails))
            {
                return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataWithItemID,
                                     KeyID, VCode, IDOrNameText);
            }

            return String.Format(CultureConstants.InvariantCulture, NetworkConstants.PostDataBase,
                                 KeyID, VCode);
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Saves the document to the disk.
        /// </summary>
        public static void SaveDocument(WebBrowser webBrowser)
        {
            if (webBrowser == null)
                throw new ArgumentNullException("webBrowser");

            if (webBrowser.Document == null || webBrowser.Document.Body == null)
                return;

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                string path = webBrowser.Url.AbsolutePath;

                sfd.Filter = "XML (*.xml)|*.xml";
                sfd.FileName = path.Substring(path.LastIndexOf("/", StringComparison.OrdinalIgnoreCase) + 1,
                                              path.LastIndexOf(".", StringComparison.OrdinalIgnoreCase) -
                                              path.LastIndexOf("/", StringComparison.OrdinalIgnoreCase) - 1);

                if (sfd.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    XmlDocument xdoc = new XmlDocument();
                    string innerText = webBrowser.Document.Body.InnerText.Trim().Replace("\n-", "\n");
                    xdoc.LoadXml(innerText);
                    string content = Util.GetXmlStringRepresentation(xdoc);

                    // Moves to the final file
                    FileHelper.OverwriteOrWarnTheUser(
                        sfd.FileName,
                        fs =>
                            {
                                using (StreamWriter writer = new StreamWriter(fs, Encoding.UTF8))
                                {
                                    writer.Write(content);
                                    writer.Flush();
                                    fs.Flush();
                                }
                                return true;
                            });
                }
                catch (IOException err)
                {
                    ExceptionHandler.LogException(err, true);
                    MessageBox.Show("There was an error writing out the file:\n\n" + err.Message,
                                    "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (XmlException err)
                {
                    ExceptionHandler.LogException(err, true);
                    MessageBox.Show("There was an error while converting to XML format.\r\nThe message was:\r\n" + err.Message,
                                    "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion
    }
}