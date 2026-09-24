/** GitHub Pages build: the static site calls the API hosted on Azure App Service (Free F1 plan). */
export const environment = {
  apiBaseUrl: 'https://asencilla-budget-api-kpk-h7akeagchccvdecu.westus3-01.azurewebsites.net',
  apiDescription: 'the demo server',
  coldStartNotice: true,
  apiUnavailableHelp: 'The demo server is not responding. It may still be waking up or be briefly offline for an update. Wait a minute, then try again.'
};
