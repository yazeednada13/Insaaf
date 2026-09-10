const expoConfig = require('eslint-config-expo/flat');
const { defineConfig } = require('eslint/config');

module.exports = defineConfig([
  expoConfig,
  {
    ignores: ['dist/*'],
  },
  {
    rules: {
      // eslint-config-expo@57 still flags async session bootstrap on mount in AuthProvider.
      'react-hooks/set-state-in-effect': 'off',
    },
  },
]);
