public interface IStudyItemMetadataProvider
{
    bool TryGetCurrentStudyItemMetadata(out StudyItemMetadata metadata);
}
