/**
 * Local development and Codespaces: the API is reached through the dev-server proxy (proxy.conf.json),
 * so requests stay relative.
 */
export const environment = {
  apiBaseUrl: '',
  apiDescription: 'the API on http://localhost:5080',
  /** Hosted demos on a free plan sleep when idle; show a notice when the first response is slow. */
  coldStartNotice: false
};
