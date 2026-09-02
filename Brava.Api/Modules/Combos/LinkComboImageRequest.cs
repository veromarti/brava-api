namespace Brava.Api.Modules.Combos;

// For attaching an image already sitting in the bucket (uploaded straight
// through the Cloudflare dashboard, say). The URL must resolve back to a key
// in our own bucket — see ComboEndpoints.LinkComboImage.
public record LinkComboImageRequest(string Url);
