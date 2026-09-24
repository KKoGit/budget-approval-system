import { environment } from '../../environments/environment';

/** Root of every API URL: relative locally (dev-server proxy), absolute when hosted on GitHub Pages. */
export const API_ROOT = `${environment.apiBaseUrl}/api`;
