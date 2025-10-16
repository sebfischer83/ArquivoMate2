declare module 'pretty-print-json' {
  export function prettyPrintJson(obj: any): string;
  export namespace prettyPrintJson {
    function toHtml(obj: any, options?: { indent?: number; quoteKeys?: boolean }): string;
  }
}

declare module 'pdfjs-dist' {
  export function getDocument(src: any): any;
  export const GlobalWorkerOptions: { workerSrc: string };
  export type PDFDocumentProxy = any;
  export type PDFPageProxy = any;
}

// allow importing internal kit component subpaths where typings exist in node_modules

