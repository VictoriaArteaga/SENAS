// Los imports ESM de NodeNext llevan extensión .js (p. ej. ../generated/prisma/client.js) aunque el
// archivo real sea .ts. Solo si la resolución normal falla se reintenta sin la extensión; así los
// paquetes de node_modules siguen resolviéndose exactamente igual.
module.exports = (request, options) => {
  try {
    return options.defaultResolver(request, options);
  } catch (error) {
    if (/^\.{1,2}\/.*\.js$/.test(request)) {
      return options.defaultResolver(request.slice(0, -3), options);
    }
    throw error;
  }
};
