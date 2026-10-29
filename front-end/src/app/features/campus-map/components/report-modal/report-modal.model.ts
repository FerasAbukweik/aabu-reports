/** An image picked for the report, with the temporary url its preview uses (revoked when it is removed) */
export interface ReportAttachment {
  file: File;
  previewUrl: string;
}
