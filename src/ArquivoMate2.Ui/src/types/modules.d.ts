// Allow importing pdf.js worker as URL via ?url suffix
declare module '*.mjs?url' {
  const url: string;
  export default url;
}
