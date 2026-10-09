const esbuild = require('esbuild');
Promise.all(['extension', 'gui-runtime'].map(name => esbuild.build({
  entryPoints: [`src/${name}.js`], bundle: true, platform: 'node', target: 'node20',
  external: ['vscode'], outfile: `dist/${name}.js`, sourcemap: false
}))).catch(() => process.exit(1));
