import { DocumentProcessingStatus } from "./document-processing-status";

export class DocumentProcessingNotification {
    documentId: string;
    status: DocumentProcessingStatus;
    message: string;
    timestamp: Date;

    constructor(documentId: string, status: DocumentProcessingStatus, message: string, timestamp?: Date) {
        this.documentId = documentId;
        this.status = status;
        this.message = message;
        this.timestamp = timestamp ?? new Date();
    }
}